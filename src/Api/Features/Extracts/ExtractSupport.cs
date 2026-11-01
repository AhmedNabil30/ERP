using System.Security.Claims;
using Kaff.Api.Authorization;
using Kaff.Api.Common.Results;
using Kaff.Domain.Authorization;
using Kaff.Domain.Boqs;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts.Billing;
using Kaff.Domain.Extracts;
using Kaff.Domain.Identity;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Extracts;

/// <summary>Everything a step needs from the request, bundled so each endpoint's handler stays one signature.</summary>
internal sealed record StepServices(
    KaffDbContext Database,
    RulingSet Rulings,
    TimeProvider Clock,
    ICurrentUser CurrentUser,
    IAuthorizationService Authorization,
    ClaimsPrincipal User);

/// <summary>One step of the chain: calls an <see cref="Extract"/> method and returns the postings it produced, if any.</summary>
internal delegate Task<Result<IReadOnlyList<Posting>>> ExtractStep(
    Extract extract, Project project, ExtractPostingContext? context, StepServices services, CancellationToken cancellationToken);

/// <summary>
/// The shared spine of every step endpoint: lock the extract row, load, run the aggregate method, and commit the
/// postings and the status change in <b>one</b> save. The permission gate (role and assignment) has already run
/// on the route; nothing here re-decides it. Audit records come from the interceptor on the same save.
/// </summary>
internal static class ExtractSupport
{
    public static DateOnly Today(TimeProvider clock) => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    /// <summary>The extract permissions the caller holds on the route's project, each asked of the real evaluator.</summary>
    public static async Task<ExtractViewer> ViewerAsync(StepServices services)
    {
        HashSet<Permission> held = [];
        foreach (Permission permission in ExtractView.Permissions)
        {
            AuthorizationResult result = await services.Authorization.AuthorizeAsync(
                services.User, resource: null, PermissionRequirement.ToPolicyName(permission, ProjectScope.FromRoute()));
            if (result.Succeeded)
            {
                held.Add(permission);
            }
        }

        return new ExtractViewer(services.CurrentUser.UserId ?? Guid.Empty, held);
    }

    /// <summary>
    /// Cumulative certified work: the sum, over the frozen BOQ's lines, of the line's rate times its executed
    /// quantity, rounded as the ruled <c>ContractValueSource</c> rounds a line total. Shared by preparation and
    /// by the revision of a rejected extract so the two can never derive it differently.
    /// </summary>
    public static Money CumulativeCertifiedWork(Boq boq, RulingSet rulings)
    {
        ArgumentNullException.ThrowIfNull(boq);
        ArgumentNullException.ThrowIfNull(rulings);

        return Money.Sum(boq.Lines.Select(line => rulings.ContractValueSource.LineTotal(line.SellRate, line.QuantityExecuted)));
    }

    public static IQueryable<Extract> WithChildren(KaffDbContext database) =>
        database.Extracts.Include(extract => extract.GateHistory).Include(extract => extract.Figures);

    /// <summary>
    /// Runs one step against an extract that belongs to <paramref name="projectId"/>, holding a row lock for the
    /// transaction so two concurrent approvals cannot both pass the state check and post twice.
    /// </summary>
    public static async Task<IResult> RunAsync(
        Guid projectId, Guid id, bool needsAccounts, StepServices services, ExtractStep step, CancellationToken cancellationToken)
    {
        KaffDbContext database = services.Database;

        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await database.Database.BeginTransactionAsync(cancellationToken);

        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM extracts WHERE id = {id} AND project_id = {projectId} FOR UPDATE", cancellationToken);

        Extract? extract = await WithChildren(database)
            .FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.ProjectId == projectId, cancellationToken);
        Project? project = await database.Projects.FirstOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken);

        if (extract is null || project is null)
        {
            return ResultExtensions.Problem(ExtractErrors.NotFound);
        }

        ExtractPostingContext? context = null;
        if (needsAccounts)
        {
            Result<ExtractPostingContext> built = await ContextAsync(database, project, services.Rulings, services.Clock, cancellationToken);
            if (built.IsFailure)
            {
                return ResultExtensions.Problem(built.Error);
            }

            context = built.Value;
        }

        Result<IReadOnlyList<Posting>> stepped = await step(extract, project, context, services, cancellationToken);
        if (stepped.IsFailure)
        {
            return ResultExtensions.Problem(stepped.Error);
        }

        IReadOnlyList<Posting> postings = stepped.Value;
        database.Postings.AddRange(postings);

        // The deferred non-negative-balance guard fires at COMMIT, so both the save and the commit are inside the
        // guard translation. Either refusal rolls the whole transaction back: no posting and no status change.
        Error? refused = await GuardedSave.SaveAsync(database, cancellationToken);
        if (refused is null)
        {
            try
            {
                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception exception) when (DatabaseGuardTranslation.Translate(exception) is { } violation)
            {
                refused = violation;
            }
        }

        if (refused is not null)
        {
            await transaction.DisposeAsync();
            return await RefusalAsync(database, refused, postings, cancellationToken);
        }

        ExtractViewer viewer = await ViewerAsync(services);
        return Microsoft.AspNetCore.Http.Results.Ok(ExtractView.Build(extract, project, services.Rulings, viewer));
    }

    /// <summary>
    /// AC-500-K. The refusal names the reason and the balance of every account the postings touched <i>as it then
    /// stands</i>, read after the rollback, so nothing the failed attempt did can show in it.
    /// </summary>
    private static async Task<IResult> RefusalAsync(
        KaffDbContext database, Error refused, IReadOnlyList<Posting> postings, CancellationToken cancellationToken)
    {
        Guid[] touched = [.. postings.SelectMany(posting => new[] { posting.FromAccountId, posting.ToAccountId }).Distinct()];
        database.ChangeTracker.Clear();

        var balances = await database.AccountBalances
            .AsNoTracking()
            .Where(balance => touched.Contains(balance.AccountId))
            .Select(balance => new { balance.AccountId, balance.AccountCode, Balance = balance.SignedBalance.Amount })
            .ToListAsync(cancellationToken);

        return ResultExtensions.Problem(
            refused,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["balances"] = balances.Select(balance => new
                {
                    accountId = balance.AccountId,
                    accountCode = balance.AccountCode,
                    balance = balance.Balance.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture),
                }).ToList(),
            });
    }

    /// <summary>
    /// The accounts an extract posts across. The four ledgers are the project's own; the safe is the company's.
    /// A Lump Sum project is not opened with a revenue account (<c>ProjectAccountSetFactory</c> lists none), so it
    /// is opened here the first time an extract needs it, through <see cref="Account.Create"/>.
    /// </summary>
    public static async Task<Result<ExtractPostingContext>> ContextAsync(
        KaffDbContext database, Project project, RulingSet rulings, TimeProvider clock, CancellationToken cancellationToken)
    {
        List<Account> own = await database.Accounts
            .Where(account => account.ProjectId == project.Id && account.IsActive)
            .ToListAsync(cancellationToken);

        Account? Pick(AccountType type) => own.FirstOrDefault(account => account.Type == type);

        Account? safe = await database.Accounts
            .Where(account => account.Type == AccountType.Safe && account.IsActive)
            .OrderBy(account => account.Code)
            .FirstOrDefaultAsync(cancellationToken);
        Account? tax = await database.Accounts
            .Where(account => account.Type == AccountType.TaxWithheldAtSource && account.IsActive)
            .OrderBy(account => account.Code)
            .FirstOrDefaultAsync(cancellationToken);

        Account? receivable = Pick(AccountType.ClientReceivable);
        Account? hold = Pick(AccountType.Hold);
        Account? clientAdvance = Pick(AccountType.ClientAdvance);
        Account? materialAdvance = Pick(AccountType.MaterialAdvance);

        if (safe is null || receivable is null || hold is null || clientAdvance is null || materialAdvance is null)
        {
            return Result.Failure<ExtractPostingContext>(ExtractErrors.AccountsMissing);
        }

        Account? revenue = Pick(AccountType.ContractRevenue);
        if (revenue is null)
        {
            Result<Account> opened = Account.Create(
                AccountType.ContractRevenue,
                $"{project.Code}-REV",
                "إيرادات المشروع",
                "Project revenue",
                project.Currency,
                Today(clock),
                projectId: project.Id);
            if (opened.IsFailure)
            {
                return Result.Failure<ExtractPostingContext>(opened.Error);
            }

            revenue = opened.Value;
            database.Accounts.Add(revenue);
        }

        return new ExtractPostingContext(
            project,
            new ExtractAccounts(revenue, receivable, hold, clientAdvance, materialAdvance, safe, tax),
            rulings,
            Today(clock));
    }

    /// <summary>
    /// The ledger balances the calculator reads, summed from the postings by the <c>account_balances</c> view.
    /// There is no stored balance to read.
    /// </summary>
    public static ExtractLedgerBalances Balances(KaffDbContext database, Guid projectId)
    {
        LedgerBalances ledgers = BalanceQueries.GetLedgerBalances(database.AccountBalances, projectId);
        return new ExtractLedgerBalances(ledgers.ClientAdvance, ledgers.MaterialAdvance, ledgers.Hold);
    }

    /// <summary>
    /// The accrual postings the extract wrote that are still standing: not reversals, and not already reversed by
    /// an earlier rejection. These are the legs <see cref="Extract.Reject"/> mirrors.
    /// </summary>
    public static async Task<IReadOnlyList<ExtractPostedLeg>> StandingAccrualLegsAsync(
        KaffDbContext database, Guid extractId, CancellationToken cancellationToken)
    {
        List<Posting> postings = await database.Postings
            .Where(posting => posting.SourceDocumentType == SourceDocumentType.Extract
                && posting.SourceDocumentId == extractId
                && posting.ReversesId == null
                && posting.Type != PostingType.ClientCollection
                && posting.Type != PostingType.TaxWithheldAtSource
                && !database.Postings.Any(other => other.ReversesId == posting.Id))
            .ToListAsync(cancellationToken);

        Guid[] accountIds = [.. postings.SelectMany(posting => new[] { posting.FromAccountId, posting.ToAccountId }).Distinct()];
        Dictionary<Guid, Account> accounts = await database.Accounts
            .Where(account => accountIds.Contains(account.Id))
            .ToDictionaryAsync(account => account.Id, cancellationToken);

        return [.. postings.Select(posting => new ExtractPostedLeg(posting, accounts[posting.FromAccountId], accounts[posting.ToAccountId]))];
    }

    public static Result<IReadOnlyList<Posting>> NoPostings() => Result.Success<IReadOnlyList<Posting>>([]);

    public static Result<IReadOnlyList<Posting>> NoPostingsOr(Result result) =>
        result.IsFailure ? Result.Failure<IReadOnlyList<Posting>>(result.Error) : NoPostings();

    public static Result<IReadOnlyList<Posting>> Of(Result<PostingSet?> result) =>
        result.IsFailure
            ? Result.Failure<IReadOnlyList<Posting>>(result.Error)
            : Result.Success<IReadOnlyList<Posting>>(result.Value is null ? [] : [.. result.Value.Postings]);
}
