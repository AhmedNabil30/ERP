using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Treasury.ReversePosting;

/// <summary>
/// Creates the correcting posting for an existing one, through <c>Posting.Reverse</c>. KAFF-303.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every rule about whether the reversal is legal goes through <c>Posting.Reverse</c>, and nothing
/// here reproduces one</b> — the mirror shape, the amount, the hold exemption (rule 5) — except the two
/// checks the entity cannot make on its own because they need to see other rows: whether the original
/// already has a reversal (rule 3, <c>AC-303-B</c>), and the closed-period rule (rule 6,
/// <c>AC-303-E</c>), both pre-checked here the same way <c>PostMovement.Handler</c> pre-checks the
/// period, with the database's own guards as the enforcement of record for the race either pre-check
/// cannot close.
/// </para>
/// <para>
/// <b><c>AC-303-C</c> — a reversal cannot itself be reversed.</b> <c>Posting.Reverse</c> does not check
/// <c>original.IsReversal</c> (see its remarks: the check lives in the database guard,
/// <c>KAFF_REVERSAL_OF_REVERSAL</c>). KAFF-319 added <see cref="TreasuryErrors.ReversalOfReversal"/> as
/// the domain-level member for that guard's message; this handler is the pre-check that returns it
/// instead of a round trip to the database.
/// </para>
/// <para>
/// <b>The route names the scope; the original posting must already live in it.</b>
/// <c>Posting.Reverse</c> always uses <c>original.ProjectId</c> — it takes no <c>projectId</c>
/// parameter of its own — so nothing downstream of this handler would notice a caller reversing a
/// project-tagged posting through the company route. The company route's permission
/// (<c>TreasuryPostCompany</c>) carries no project-assignment check at all, so skipping this would let
/// a company-wide Finance user reverse any project's posting without ever being assigned to it —
/// exactly the hole <c>AC-303-F</c> exists to close. Comparing the route's project (<c>null</c> on the
/// company route) against <c>original.ProjectId</c> here is what keeps the two routes as strict as
/// <c>PostMovement</c>'s.
/// </para>
/// <para>
/// <b>No audit record is written here.</b> Same as <c>PostMovement.Handler</c>: the reversal is an
/// ordinary tracked <c>Posting</c>, so <c>AuditSaveChangesInterceptor</c> writes the <c>Created</c>
/// record in the same transaction as the save.
/// </para>
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid? projectId,
        Guid id,
        Request request,
        KaffDbContext database,
        ICurrentUser currentUser,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(currentUser);

        Posting? original = await database.Postings
            .FirstOrDefaultAsync(posting => posting.Id == id, cancellationToken);

        if (original is null || original.ProjectId != projectId)
        {
            // Same refusal either way: a posting outside the caller's route/permission scope must not
            // be distinguishable from one that does not exist at all (AC-303-F).
            return ResultExtensions.Problem(TreasuryErrors.ReversalTargetNotFound);
        }

        // Rule 4 / AC-303-C: a reversal cannot itself be reversed. Posting.Reverse does not check this
        // — only the database guard does — so this is the domain-level pre-check KAFF-319 added the
        // error member for.
        if (original.IsReversal)
        {
            return ResultExtensions.Problem(TreasuryErrors.ReversalOfReversal);
        }

        // Rule 3 / AC-303-B: a posting can be reversed once only. The database's unique index on
        // reverses_id is the enforcement of record; this is the friendly pre-check.
        bool alreadyReversed = await database.Postings
            .AnyAsync(posting => posting.ReversesId == original.Id, cancellationToken);

        if (alreadyReversed)
        {
            return ResultExtensions.Problem(TreasuryErrors.PostingAlreadyReversed);
        }

        Account? from = await database.Accounts
            .FirstOrDefaultAsync(account => account.Id == original.FromAccountId, cancellationToken);

        Account? to = await database.Accounts
            .FirstOrDefaultAsync(account => account.Id == original.ToAccountId, cancellationToken);

        if (from is null || to is null)
        {
            return ResultExtensions.Problem(TreasuryErrors.AccountNotFound);
        }

        // Rule 6 / AC-303-E: a reversal must date into an open accounting period like any other
        // posting. The domain cannot see periods — same pre-check PostMovement.Handler runs.
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

        Result<Posting> reversed = Posting.Reverse(
            original,
            from,
            to,
            request.PostingDate,
            currentUser.UserId ?? Guid.Empty,
            clock.GetUtcNow());

        if (reversed.IsFailure)
        {
            return ResultExtensions.Problem(reversed.Error);
        }

        Posting reversal = reversed.Value;
        database.Postings.Add(reversal);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (GuardViolation(exception) is { } violation)
        {
            // Same race guard as PostMovement.Handler: a period can close, or a concurrent reversal of
            // the same original can land, between the pre-checks above and this insert. The database's
            // own guards are the enforcement of record either way.
            return ResultExtensions.Problem(violation);
        }

        return Microsoft.AspNetCore.Http.Results.Created(
            $"/api/treasury/postings/{reversal.Id}",
            new Response(
                reversal.Id,
                reversal.PostingDate,
                reversal.FromAccountId,
                reversal.ToAccountId,
                reversal.Amount,
                reversal.Type,
                reversal.ProjectId,
                reversal.ReversesId!.Value));
    }

    private static Error? GuardViolation(Exception exception) => DatabaseGuardTranslation.Translate(exception);
}
