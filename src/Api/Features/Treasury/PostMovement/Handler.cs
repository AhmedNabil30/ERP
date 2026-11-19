using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

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
        RulingSet rulings,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(rulings);
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(currentUser);

        // The validator refuses a missing date (TypedPostingDate); nothing here may default it.
        DateOnly postingDate = request.PostingDate!.Value;

        // A flow's money moves only through its flow (D-237, O-r2-1): refused before any other check.
        Result movement = Posting.CheckMovement(request.Type, request.SourceDocumentType);
        if (movement.IsFailure)
        {
            return ResultExtensions.Problem(movement.Error);
        }

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
                          && period.StartsOn <= postingDate
                          && period.EndsOn >= postingDate,
                cancellationToken);

        if (periodClosed)
        {
            return ResultExtensions.Problem(TreasuryErrors.ClosedPeriod);
        }

        if (projectId is { } taggedProject)
        {
            // The domain's Posting cannot see a project, so the state check is made here, from the
            // table in the rulings module. A company-level posting names no project and skips it.
            ProjectStatus? status = await database.Projects
                .Where(project => project.Id == taggedProject)
                .Select(project => (ProjectStatus?)project.Status)
                .FirstOrDefaultAsync(cancellationToken);

            if (status is null)
            {
                return ResultExtensions.Problem(ProjectErrors.NotFound);
            }

            Result stateCheck = PostingProjectStateLegality.Validate(
                rulings.PostingProjectStates, status.Value, request.Type, isReversal: false);

            if (stateCheck.IsFailure)
            {
                return ResultExtensions.Problem(stateCheck.Error);
            }
        }

        Result<Posting> created = Posting.CreateMovement(
            from,
            to,
            request.Amount,
            request.Type,
            new SourceDocument(request.SourceDocumentType, request.SourceDocumentId, request.SourceDocumentReference),
            postingDate,
            currentUser.UserId ?? Guid.Empty,
            clock.GetUtcNow(),
            projectId);

        if (created.IsFailure)
        {
            return ResultExtensions.Problem(created.Error);
        }

        Posting posting = created.Value;
        database.Postings.Add(posting);

        // The period pre-check above is not the enforcement: a period can close between the check and
        // the insert, and the safe floor has no pre-check at all. GuardedSave turns any named database
        // guard into the same translated refusal everyone else gets (KAFF-319), and rethrows the rest.
        Error? refused = await GuardedSave.SaveAsync(database, cancellationToken);

        if (refused is not null)
        {
            return ResultExtensions.Problem(refused);
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
}
