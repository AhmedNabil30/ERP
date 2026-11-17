using Kaff.Api.Common.Results;
using Kaff.Domain.Closure;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Closure.ReleaseSubcontractorRetention;

/// <summary>What was released.</summary>
/// <param name="ProjectId">The project.</param>
/// <param name="SubcontractorId">The subcontractor.</param>
/// <param name="Released">The retention balance at the moment of release, summed from postings. Never typed.</param>
/// <param name="PostingIds">The release posting.</param>
public sealed record Response(Guid ProjectId, Guid SubcontractorId, Money Released, IReadOnlyList<Guid> PostingIds);

/// <summary>
/// Releases a subcontractor's retention. The ledger is that subcontractor's own SubcontractorRetention account, which
/// is none of the five ledgers and is never the client hold. The amount is read from it inside the transaction that
/// holds the project row lock; the project state, the warranty end and the nothing-to-release rule are checked by
/// <see cref="SubcontractorRetention.Release"/>, not here.
/// </summary>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        Guid subcontractorId,
        KaffDbContext database,
        RulingSet rulings,
        TimeProvider clock,
        ICurrentUser currentUser,
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

        List<Account> own = await database.Accounts
            .Where(account => account.ProjectId == projectId && account.PartyId == subcontractorId && account.IsActive)
            .ToListAsync(cancellationToken);
        Account? retention = own.FirstOrDefault(account => account.Type == AccountType.SubcontractorRetention);
        Account? payable = own.FirstOrDefault(account => account.Type == AccountType.SubcontractorPayable);

        Money retained = retention is null
            ? Money.Zero
            : BalanceQueries.GetAccountBalance(database.AccountBalances, retention.Id)?.SignedBalance ?? Money.Zero;

        DateTimeOffset now = clock.GetUtcNow();
        Result<PostingSet> built = SubcontractorRetention.Release(
            project,
            subcontractorId,
            retention,
            payable,
            retained,
            currentUser.UserId ?? Guid.Empty,
            now,
            SiteTime.DateOf(now),
            rulings.PostingProjectStates);
        if (built.IsFailure)
        {
            return ResultExtensions.Problem(built.Error);
        }

        database.Postings.AddRange(built.Value.Postings);

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

        return refused is not null
            ? ResultExtensions.Problem(refused)
            : Microsoft.AspNetCore.Http.Results.Ok(
                new Response(projectId, subcontractorId, retained, [.. built.Value.Postings.Select(posting => posting.Id)]));
    }
}
