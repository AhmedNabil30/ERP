using Kaff.Api.Common.Results;
using Kaff.Domain.Closure;
using Kaff.Domain.Common;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Closure;

/// <summary>What a closure act returns to the shared runner: the domain result and any detail a refusal must carry.</summary>
internal sealed record ClosureOutcome(Result Result, IReadOnlyDictionary<string, object?>? RefusalDetail = null)
{
    public static ClosureOutcome Of(Result result) => new(result);
}

/// <summary>What a hold release request or approval is checked against. Every figure is read, none is typed.</summary>
internal sealed record HoldReleaseInputs(Account? Hold, Account? Receivable, Money HoldBalance, bool AlreadyReleased);

/// <summary>What a retention release request or approval is checked against. Every figure is read, none is typed.</summary>
internal sealed record RetentionReleaseInputs(Account? Retention, Account? Payable, Money Retained);

/// <summary>The project after a closure act.</summary>
public sealed record ProjectClosureResponse(
    Guid Id, ProjectStatus Status, DateOnly? HandoverOn, DateOnly? WarrantyEndsOn, DateOnly? ClosedOn)
{
    internal static ProjectClosureResponse From(Project project) =>
        new(project.Id, project.Status, project.HandoverOn, project.WarrantyEndsOn, project.ClosedOn);
}

/// <summary>
/// The shared spine of the closure acts on a project: lock the project row, run the act, and commit the change in one save.
/// The permission gate (role and assignment) has already run on the route. Audit records come from the interceptor on the
/// same save.
/// </summary>
internal static class ClosureSupport
{
    public static DateOnly Today(TimeProvider clock) => SiteTime.Today(clock);

    public static async Task<IResult> RunAsync(
        Guid projectId,
        KaffDbContext database,
        Func<Project, CancellationToken, Task<ClosureOutcome>> act,
        CancellationToken cancellationToken)
    {
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await database.Database.BeginTransactionAsync(cancellationToken);

        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM projects WHERE id = {projectId} FOR UPDATE", cancellationToken);

        Project? project = await database.Projects.FirstOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken);
        if (project is null)
        {
            return ResultExtensions.Problem(ProjectErrors.NotFound);
        }

        ClosureOutcome outcome = await act(project, cancellationToken);
        if (outcome.Result.IsFailure)
        {
            return ResultExtensions.Problem(outcome.Result.Error, outcome.RefusalDetail);
        }

        Error? refused = await GuardedSave.SaveAsync(database, cancellationToken);
        if (refused is not null)
        {
            return ResultExtensions.Problem(refused);
        }

        await transaction.CommitAsync(cancellationToken);
        return Microsoft.AspNetCore.Http.Results.Ok(ProjectClosureResponse.From(project));
    }

    /// <summary>Loads one snag of the route's project, runs the step and saves. Audit records come from the interceptor.</summary>
    public static async Task<IResult> MutateSnagAsync(
        Guid projectId, Guid id, KaffDbContext database, Func<Snag, Result> step, CancellationToken cancellationToken)
    {
        Snag? snag = await database.Snags.FirstOrDefaultAsync(
            candidate => candidate.Id == id && candidate.ProjectId == projectId, cancellationToken);
        if (snag is null)
        {
            return ResultExtensions.Problem(ClosureErrors.SnagNotFound);
        }

        Result stepped = step(snag);
        if (stepped.IsFailure)
        {
            return ResultExtensions.Problem(stepped.Error);
        }

        Error? refused = await GuardedSave.SaveAsync(database, cancellationToken);
        return refused is not null
            ? ResultExtensions.Problem(refused)
            : Microsoft.AspNetCore.Http.Results.Ok(SnagResponse.From(snag));
    }

    /// <summary>
    /// What the hold release is checked against, read inside the caller's transaction under the project row lock: the
    /// hold and receivable accounts, the hold balance summed from postings, and whether a standing release exists. A
    /// release reversed before 2026-11-18, when the generic reversal route still allowed it, still counts as the release.
    /// </summary>
    public static async Task<HoldReleaseInputs> ReadHoldReleaseInputsAsync(
        KaffDbContext database, Guid projectId, CancellationToken cancellationToken)
    {
        List<Account> own = await database.Accounts
            .Where(account => account.ProjectId == projectId && account.IsActive)
            .ToListAsync(cancellationToken);

        bool alreadyReleased = await database.Postings.AnyAsync(
            posting => posting.ProjectId == projectId
                && posting.Type == PostingType.HoldRelease
                && posting.ReversesId == null,
            cancellationToken);

        return new HoldReleaseInputs(
            own.FirstOrDefault(account => account.Type == AccountType.Hold),
            own.FirstOrDefault(account => account.Type == AccountType.ClientReceivable),
            BalanceQueries.GetLedgerBalances(database.AccountBalances, projectId).Hold,
            alreadyReleased);
    }

    /// <summary>The request waiting for the Owner on the project, if any. At most one exists (a filtered unique index).</summary>
    public static Task<HoldReleaseRequest?> WaitingHoldReleaseAsync(
        KaffDbContext database, Guid projectId, CancellationToken cancellationToken) =>
        database.HoldReleaseRequests.FirstOrDefaultAsync(
            request => request.ProjectId == projectId && request.Status == HoldReleaseRequestStatus.Requested,
            cancellationToken);

    /// <summary>
    /// What a retention release request or approval is checked against, read inside the caller's transaction under the
    /// project row lock: the subcontractor's retention and payable accounts and the retention balance summed from postings.
    /// </summary>
    public static async Task<RetentionReleaseInputs> ReadRetentionReleaseInputsAsync(
        KaffDbContext database, Guid projectId, Guid subcontractorId, CancellationToken cancellationToken)
    {
        List<Account> own = await database.Accounts
            .Where(account => account.ProjectId == projectId && account.PartyId == subcontractorId && account.IsActive)
            .ToListAsync(cancellationToken);
        Account? retention = own.FirstOrDefault(account => account.Type == AccountType.SubcontractorRetention);
        Account? payable = own.FirstOrDefault(account => account.Type == AccountType.SubcontractorPayable);
        Money retained = retention is null
            ? Money.Zero
            : BalanceQueries.GetAccountBalance(database.AccountBalances, retention.Id)?.SignedBalance ?? Money.Zero;
        return new RetentionReleaseInputs(retention, payable, retained);
    }

    /// <summary>The request waiting for the Owner for one subcontractor on the project, if any. At most one exists (a filtered unique index).</summary>
    public static Task<SubcontractorRetentionReleaseRequest?> WaitingRetentionReleaseAsync(
        KaffDbContext database, Guid projectId, Guid subcontractorId, CancellationToken cancellationToken) =>
        database.SubcontractorRetentionReleaseRequests.FirstOrDefaultAsync(
            request => request.ProjectId == projectId
                && request.SubcontractorId == subcontractorId
                && request.Status == HoldReleaseRequestStatus.Requested,
            cancellationToken);

    /// <summary>The next number in a project's own sequence; the unique index on (project, number) is the enforcement of record.</summary>
    public static async Task<int> NextSnagNumberAsync(KaffDbContext database, Guid projectId, CancellationToken cancellationToken) =>
        (await database.Snags.Where(snag => snag.ProjectId == projectId)
            .Select(snag => (int?)snag.Number).MaxAsync(cancellationToken) ?? 0) + 1;

    public static async Task<int> NextCallbackNumberAsync(KaffDbContext database, Guid projectId, CancellationToken cancellationToken) =>
        (await database.WarrantyCallbacks.Where(callback => callback.ProjectId == projectId)
            .Select(callback => (int?)callback.Number).MaxAsync(cancellationToken) ?? 0) + 1;
}
