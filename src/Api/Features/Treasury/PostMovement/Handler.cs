using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kaff.Api.Features.Treasury.PostMovement;

/// <summary>
/// Creates one <c>Posting</c> between two named accounts. KAFF-301.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every business rule about whether the movement is legal goes through <c>Posting.Create</c>, and
/// nothing here reproduces one.</b> Positive amount, distinct accounts, matching currency, the five
/// ledgers never netting, the hold only growing, and the project tag matching the accounts it moves
/// between — all of it lives there (<c>rules 1, 3, 5, 6, 7</c>). This handler loads the two accounts,
/// calls it, and returns whatever it says. The database re-checks the same rules independently,
/// plus the balance floor and the closed-period rule, which need to see the whole ledger — rule 10:
/// if the two ever disagree, the database wins and the domain has a bug.
/// </para>
/// <para>
/// <b>The project is not checked against the caller's assignment here.</b> <c>Permission.TreasuryPostProject</c>
/// is project-scoped, so a caller only reaches this method after <c>IProjectAccessPolicy</c> granted
/// access to the project the route names (<c>AC-301-I</c>) — the same mechanism
/// <c>AssignUserToProject</c> uses for the identical rule.
/// </para>
/// <para>
/// <b>The closed-period check (rule 9) is not in <c>Posting.Create</c></b> — the domain has no notion
/// of an accounting period, only the database does (<c>001_guards.sql</c>'s <c>kaff_postings_validate</c>).
/// This handler pre-checks the period so a closed month gets a translated refusal rather than a round
/// trip to the database's own guard; the <c>catch</c> below is the safety net for the race between that
/// check and the insert, exactly like <c>AssignUserToProject</c>'s duplicate-assignment race guard.
/// </para>
/// <para>
/// <b>No audit record is written here.</b> <c>Posting</c> is an ordinary tracked entity, so
/// <c>AuditSaveChangesInterceptor</c> writes the <c>Created</c> record — who, when, the full row — in
/// the same transaction as the save. A hand-written record here would be the second mechanism
/// CLAUDE.md forbids.
/// </para>
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid? projectId,
        Request request,
        KaffDbContext database,
        ICurrentUser currentUser,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(currentUser);

        Account? from = await database.Accounts
            .FirstOrDefaultAsync(account => account.Id == request.FromAccountId, cancellationToken);

        if (from is null)
        {
            return ResultExtensions.Problem(TreasuryErrors.AccountNotFound);
        }

        Account? to = await database.Accounts
            .FirstOrDefaultAsync(account => account.Id == request.ToAccountId, cancellationToken);

        if (to is null)
        {
            return ResultExtensions.Problem(TreasuryErrors.AccountNotFound);
        }

        // Rule 9: a closed period is immutable. The domain cannot see this — only the database can,
        // through the ledger-wide guard — so this is a friendly pre-check, not the enforcement.
        bool periodClosed = await database.AccountingPeriods
            .AnyAsync(
                period => period.Status == PeriodStatus.Closed
                          && period.StartsOn <= request.PostingDate
                          && period.EndsOn >= request.PostingDate,
                cancellationToken);

        if (periodClosed)
        {
            return ResultExtensions.Problem(TreasuryErrors.ClosedPeriod);
        }

        Result<Posting> created = Posting.Create(
            from,
            to,
            request.Amount,
            request.Type,
            new SourceDocument(request.SourceDocumentType, request.SourceDocumentId, request.SourceDocumentReference),
            request.PostingDate,
            currentUser.UserId ?? Guid.Empty,
            clock.GetUtcNow(),
            projectId);

        if (created.IsFailure)
        {
            return ResultExtensions.Problem(created.Error);
        }

        Posting posting = created.Value;
        database.Postings.Add(posting);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsClosedPeriodGuard(exception))
        {
            // The pre-check above is not the enforcement: a period can close between the check and the
            // insert. The loser of that race gets the same translated refusal as everyone else rather
            // than a raw PostgresException surfacing as a 500 — KAFF-319 generalises this mapping for
            // every guard; this is the one rule this story's own AC-301-H requires today.
            return ResultExtensions.Problem(TreasuryErrors.ClosedPeriod);
        }

        return Microsoft.AspNetCore.Http.Results.Created(
            $"/api/treasury/postings/{posting.Id}",
            new Response(
                posting.Id,
                posting.PostingDate,
                posting.FromAccountId,
                posting.ToAccountId,
                posting.Amount,
                posting.Type,
                posting.ProjectId,
                posting.IsReversal));
    }

    /// <summary>The <c>KAFF_CLOSED_PERIOD</c> guard, raised by <c>kaff_postings_validate</c>, and nothing else.</summary>
    private static bool IsClosedPeriodGuard(DbUpdateException exception)
        => exception.InnerException is PostgresException postgres
           && postgres.MessageText.Contains("KAFF_CLOSED_PERIOD", StringComparison.Ordinal);
}
