using System.Text.Json;
using Kaff.Api.Common.Results;
using Kaff.Domain.Accounting;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Accounting;

public sealed record AccrualPayload(Guid AccruedExpenseAccountId, Guid ExpenseAccountId, decimal Amount, DateOnly? AccruedOn, string? Description);

public sealed record AccrualReleasePayload(Guid AccrualId, DateOnly? ReleasedOn);

public sealed record MonthPayload(DateOnly? Month);

/// <summary>What Finance sees after asking: the request waits for the Owner, and nothing has posted.</summary>
public sealed record RequestResponse(Guid RequestId, AccountingRequestKind Kind, AccountingRequestStatus Status, Guid? ProjectId);

/// <summary>
/// The one place the accounting entries are built. Finance's request runs it as a dry run (nothing added to the context, so a bad
/// request is refused at once); the Owner's approval runs it again and adds what it built. KAFF-700, KAFF-701.
/// </summary>
internal static class AccountingEntries
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Builds the request, proves it would post, stores it and answers 201. Nothing posts.</summary>
    public static async Task<IResult> SubmitAsync<TPayload>(
        AccountingRequestKind kind,
        Guid? projectId,
        TPayload payload,
        KaffDbContext database,
        ICurrentUser currentUser,
        RulingSet rulings,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(currentUser);

        Guid actor = currentUser.UserId ?? Guid.Empty;
        DateTimeOffset now = clock.GetUtcNow();
        string json = JsonSerializer.Serialize(payload, Json);

        Result<IReadOnlyList<object>> dryRun = await BuildAsync(kind, projectId, json, actor, database, rulings, now, track: false, cancellationToken);
        if (dryRun.IsFailure)
        {
            return ResultExtensions.Problem(dryRun.Error);
        }

        Result<AccountingRequest> created = AccountingRequest.Create(kind, projectId, json, actor, now);
        if (created.IsFailure)
        {
            return ResultExtensions.Problem(created.Error);
        }

        database.AccountingRequests.Add(created.Value);

        Error? guard = await GuardedSave.SaveAsync(database, cancellationToken);
        if (guard is not null)
        {
            return ResultExtensions.Problem(guard);
        }

        return Microsoft.AspNetCore.Http.Results.Created(
            $"/api/treasury/accounting-requests/{created.Value.Id}",
            new RequestResponse(created.Value.Id, kind, created.Value.Status, projectId));
    }

    /// <summary>
    /// The entities (accruals, postings) the request produces now. <paramref name="track"/> is false for the dry run: nothing is
    /// loaded as a tracked entity that the build could change.
    /// </summary>
    public static async Task<Result<IReadOnlyList<object>>> BuildAsync(
        AccountingRequestKind kind,
        Guid? projectId,
        string payloadJson,
        Guid requester,
        KaffDbContext database,
        RulingSet rulings,
        DateTimeOffset now,
        bool track,
        CancellationToken cancellationToken)
    {
        DateOnly today = SiteTime.DateOf(now);

        return kind switch
        {
            AccountingRequestKind.Accrual => await RecordAccrualAsync(projectId, Read<AccrualPayload>(payloadJson), requester, database, rulings, now, today, cancellationToken),
            AccountingRequestKind.AccrualRelease => await ReleaseAccrualAsync(projectId, Read<AccrualReleasePayload>(payloadJson), requester, database, rulings, now, today, track, cancellationToken),
            AccountingRequestKind.PrepaymentAmortisation => await AmortiseAsync(projectId, Read<MonthPayload>(payloadJson), requester, database, rulings, now, today, cancellationToken),
            AccountingRequestKind.Depreciation => await DepreciateAsync(Read<MonthPayload>(payloadJson), requester, database, rulings, now, today, cancellationToken),
            _ => Result.Failure<IReadOnlyList<object>>(AccountingErrors.AccountWrongType),
        };
    }

    private static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, Json)!;

    private static async Task<Result<IReadOnlyList<object>>> RecordAccrualAsync(
        Guid? projectId, AccrualPayload payload, Guid requester, KaffDbContext database, RulingSet rulings, DateTimeOffset now, DateOnly today, CancellationToken cancellationToken)
    {
        Result dated = TypedPostingDate.Check(payload.AccruedOn, today);
        if (dated.IsFailure)
        {
            return Result.Failure<IReadOnlyList<object>>(dated.Error);
        }

        DateOnly accruedOn = payload.AccruedOn!.Value;

        Account? accrued = await database.Accounts.AsNoTracking().FirstOrDefaultAsync(account => account.Id == payload.AccruedExpenseAccountId, cancellationToken);
        Account? expense = await database.Accounts.AsNoTracking().FirstOrDefaultAsync(account => account.Id == payload.ExpenseAccountId, cancellationToken);

        if (accrued is null || expense is null)
        {
            return Result.Failure<IReadOnlyList<object>>(TreasuryErrors.AccountNotFound);
        }

        Error? refused = await AccountingSupport.ClosedPeriodAsync(database, accruedOn, cancellationToken)
            ?? await AccountingSupport.ProjectStateAsync(database, projectId, PostingType.ExpenseAccrual, rulings, cancellationToken);

        if (refused is not null)
        {
            return Result.Failure<IReadOnlyList<object>>(refused);
        }

        Result<AccrualWithPosting> created = Accrual.Create(
            projectId, accrued, expense, new Money(payload.Amount), accruedOn, payload.Description, requester, now);

        return created.IsFailure
            ? Result.Failure<IReadOnlyList<object>>(created.Error)
            : Result.Success<IReadOnlyList<object>>([created.Value.Accrual, created.Value.Posting]);
    }

    private static async Task<Result<IReadOnlyList<object>>> ReleaseAccrualAsync(
        Guid? projectId, AccrualReleasePayload payload, Guid requester, KaffDbContext database, RulingSet rulings, DateTimeOffset now, DateOnly today, bool track, CancellationToken cancellationToken)
    {
        Result dated = TypedPostingDate.Check(payload.ReleasedOn, today);
        if (dated.IsFailure)
        {
            return Result.Failure<IReadOnlyList<object>>(dated.Error);
        }

        DateOnly releasedOn = payload.ReleasedOn!.Value;

        // A project accrual is reached only through its own project, a company accrual only through the company tag.
        IQueryable<Accrual> accruals = track ? database.Accruals : database.Accruals.AsNoTracking();
        Accrual? accrual = await accruals.FirstOrDefaultAsync(candidate => candidate.Id == payload.AccrualId && candidate.ProjectId == projectId, cancellationToken);

        if (accrual is null)
        {
            return Result.Failure<IReadOnlyList<object>>(AccountingErrors.AccrualNotFound);
        }

        Account? accrued = await database.Accounts.AsNoTracking().FirstOrDefaultAsync(account => account.Id == accrual.AccruedExpenseAccountId, cancellationToken);
        Account? expense = await database.Accounts.AsNoTracking().FirstOrDefaultAsync(account => account.Id == accrual.ExpenseAccountId, cancellationToken);

        if (accrued is null || expense is null)
        {
            return Result.Failure<IReadOnlyList<object>>(TreasuryErrors.AccountNotFound);
        }

        Error? refused = await AccountingSupport.ClosedPeriodAsync(database, releasedOn, cancellationToken)
            ?? await AccountingSupport.ProjectStateAsync(database, projectId, PostingType.AccrualRelease, rulings, cancellationToken);

        if (refused is not null)
        {
            return Result.Failure<IReadOnlyList<object>>(refused);
        }

        Result<Posting> posting = accrual.Release(accrued, expense, releasedOn, requester, now);

        return posting.IsFailure
            ? Result.Failure<IReadOnlyList<object>>(posting.Error)
            : Result.Success<IReadOnlyList<object>>([posting.Value]);
    }

    private static async Task<Result<IReadOnlyList<object>>> AmortiseAsync(
        Guid? projectId, MonthPayload payload, Guid requester, KaffDbContext database, RulingSet rulings, DateTimeOffset now, DateOnly today, CancellationToken cancellationToken)
    {
        Result<DateOnly> month = AccountingSupport.MonthToCharge(payload.Month, today);
        if (month.IsFailure)
        {
            return Result.Failure<IReadOnlyList<object>>(month.Error);
        }

        DateOnly postingDate = AccountingSupport.ChargeDate(month.Value, today);

        Error? refused = await AccountingSupport.ClosedPeriodAsync(database, postingDate, cancellationToken)
            ?? await AccountingSupport.ProjectStateAsync(database, projectId, PostingType.PrepaymentAmortisation, rulings, cancellationToken);

        if (refused is not null)
        {
            return Result.Failure<IReadOnlyList<object>>(refused);
        }

        List<PrepaymentSchedule> schedules = await database.PrepaymentSchedules.AsNoTracking()
            .Where(schedule => schedule.ProjectId == projectId)
            .OrderBy(schedule => schedule.Id)
            .ToListAsync(cancellationToken);

        List<Guid> scheduleIds = [.. schedules.Select(schedule => schedule.Id)];
        List<Guid> accountIds = [.. schedules.SelectMany(schedule => new[] { schedule.PrepaymentAccountId, schedule.ExpenseAccountId }).Distinct()];

        Dictionary<Guid, Account> accounts = await database.Accounts.AsNoTracking()
            .Where(account => accountIds.Contains(account.Id))
            .ToDictionaryAsync(account => account.Id, cancellationToken);

        List<Posting> existing = await database.Postings.AsNoTracking()
            .Where(posting => posting.Type == PostingType.PrepaymentAmortisation
                              && posting.SourceDocumentType == SourceDocumentType.CompanyExpense
                              && scheduleIds.Contains(posting.SourceDocumentId))
            .ToListAsync(cancellationToken);

        List<object> built = [];

        foreach (PrepaymentSchedule schedule in schedules)
        {
            // A schedule outside this month is simply not due: it is not an error for the run as a whole.
            int index = ((month.Value.Year - schedule.FirstMonth.Year) * 12) + month.Value.Month - schedule.FirstMonth.Month;
            if (index < 0 || index >= schedule.Months)
            {
                continue;
            }

            IReadOnlyList<Posting> active = ScheduleLedger.Active(existing.Where(posting => posting.SourceDocumentId == schedule.Id));

            Result<Posting?> charged = schedule.Amortise(
                month.Value, active, postingDate, accounts[schedule.PrepaymentAccountId], accounts[schedule.ExpenseAccountId], requester, now);

            if (charged.IsFailure)
            {
                return Result.Failure<IReadOnlyList<object>>(charged.Error);
            }

            if (charged.Value is { } posting)
            {
                built.Add(posting);
            }
        }

        return Result.Success<IReadOnlyList<object>>(built);
    }

    private static async Task<Result<IReadOnlyList<object>>> DepreciateAsync(
        MonthPayload payload, Guid requester, KaffDbContext database, RulingSet rulings, DateTimeOffset now, DateOnly today, CancellationToken cancellationToken)
    {
        Result<DateOnly> month = AccountingSupport.MonthToCharge(payload.Month, today);
        if (month.IsFailure)
        {
            return Result.Failure<IReadOnlyList<object>>(month.Error);
        }

        DateOnly postingDate = AccountingSupport.ChargeDate(month.Value, today);

        Error? closed = await AccountingSupport.ClosedPeriodAsync(database, postingDate, cancellationToken);
        if (closed is not null)
        {
            return Result.Failure<IReadOnlyList<object>>(closed);
        }

        List<AssetRegisterEntry> assets = await database.AssetRegisterEntries.AsNoTracking().OrderBy(asset => asset.Id).ToListAsync(cancellationToken);
        List<Guid> assetIds = [.. assets.Select(asset => asset.Id)];
        List<Guid> accountIds = [.. assets.SelectMany(asset => new[] { asset.AccumulatedDepreciationAccountId, asset.DepreciationExpenseAccountId }).Distinct()];

        Dictionary<Guid, Account> accounts = await database.Accounts.AsNoTracking()
            .Where(account => accountIds.Contains(account.Id))
            .ToDictionaryAsync(account => account.Id, cancellationToken);

        List<Posting> existing = await database.Postings.AsNoTracking()
            .Where(posting => posting.Type == PostingType.Depreciation
                              && posting.SourceDocumentType == SourceDocumentType.AssetRegisterEntry
                              && assetIds.Contains(posting.SourceDocumentId))
            .ToListAsync(cancellationToken);

        List<object> built = [];

        foreach (AssetRegisterEntry asset in assets)
        {
            // An asset whose first charge month is later is not due yet: skipped, not an error for the run.
            if (month.Value < asset.FirstChargeMonth(rulings.AssetDepreciation))
            {
                continue;
            }

            IReadOnlyList<Posting> active = ScheduleLedger.Active(existing.Where(posting => posting.SourceDocumentId == asset.Id));

            Result<DepreciationCharge> charged = asset.Depreciate(
                month.Value, active, postingDate,
                accounts[asset.AccumulatedDepreciationAccountId], accounts[asset.DepreciationExpenseAccountId],
                rulings.AssetDepreciation, requester, now);

            if (charged.IsFailure)
            {
                return Result.Failure<IReadOnlyList<object>>(charged.Error);
            }

            if (charged.Value.Posting is { } posting)
            {
                built.Add(posting);
            }
        }

        return Result.Success<IReadOnlyList<object>>(built);
    }
}
