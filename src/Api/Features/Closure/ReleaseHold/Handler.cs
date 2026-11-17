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

namespace Kaff.Api.Features.Closure.ReleaseHold;

/// <summary>What was released.</summary>
/// <param name="ProjectId">The project.</param>
/// <param name="Released">The hold balance at the moment of release, summed from postings. Never typed.</param>
/// <param name="PostingIds">The release posting.</param>
public sealed record Response(Guid ProjectId, Money Released, IReadOnlyList<Guid> PostingIds);

/// <summary>
/// The single legal debit of the hold. The amount is read from the hold ledger inside the transaction that holds the
/// project row lock; the project state, the "once" rule and the contract type are checked by
/// <see cref="ProjectClosure.ReleaseHold"/>, not here. The database independently requires the release to leave the hold
/// at exactly zero.
/// </summary>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
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
            .Where(account => account.ProjectId == projectId && account.IsActive)
            .ToListAsync(cancellationToken);

        Money holdBalance = BalanceQueries.GetLedgerBalances(database.AccountBalances, projectId).Hold;

        bool alreadyReleased = await database.Postings.AnyAsync(
            posting => posting.ProjectId == projectId
                && posting.Type == PostingType.HoldRelease
                && posting.ReversesId == null
                && !database.Postings.Any(other => other.ReversesId == posting.Id),
            cancellationToken);

        DateTimeOffset now = clock.GetUtcNow();
        Result<PostingSet> built = ProjectClosure.ReleaseHold(
            project,
            own.FirstOrDefault(account => account.Type == AccountType.Hold),
            own.FirstOrDefault(account => account.Type == AccountType.ClientReceivable),
            holdBalance,
            alreadyReleased,
            currentUser.UserId ?? Guid.Empty,
            now,
            SiteTime.DateOf(now),
            rulings.PostingProjectStates);

        if (built.IsFailure)
        {
            return ResultExtensions.Problem(built.Error);
        }

        database.Postings.AddRange(built.Value.Postings);

        // The exact-zero guard fires on the save or at COMMIT, so both are inside the translation.
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
            : Microsoft.AspNetCore.Http.Results.Ok(new Response(projectId, holdBalance, [.. built.Value.Postings.Select(posting => posting.Id)]));
    }
}
