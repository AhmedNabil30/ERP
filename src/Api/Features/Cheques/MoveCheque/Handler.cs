using Kaff.Api.Common.Results;
using Kaff.Api.Features.Extracts;
using Kaff.Domain.Closure;
using Kaff.Domain.Collections;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Cheques.MoveCheque;

/// <summary>The cheque's new state. Its own state machine refuses every other move.</summary>
/// <param name="Id">The cheque.</param>
/// <param name="State">The state it is now in.</param>
public sealed record Response(Guid Id, ChequeState State);

internal static class Handler
{
    /// <summary>Received to Deposited. Writes audit and posts nothing: a deposited cheque is not yet cash.</summary>
    public static async Task<IResult> DepositAsync(
        Guid projectId,
        Guid id,
        KaffDbContext database,
        TimeProvider clock,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        Cheque? cheque = await database.Cheques
            .FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.ProjectId == projectId, cancellationToken);
        if (cheque is null)
        {
            return ResultExtensions.Problem(CollectionErrors.ChequeNotFound);
        }

        Result moved = cheque.MoveTo(ChequeState.Deposited, currentUser.UserId ?? Guid.Empty, clock.GetUtcNow());
        if (moved.IsFailure)
        {
            return ResultExtensions.Problem(moved.Error);
        }

        await database.SaveChangesAsync(cancellationToken);
        return Microsoft.AspNetCore.Http.Results.Ok(new Response(cheque.Id, cheque.State));
    }

    /// <summary>
    /// Deposited to Cleared or Bounced, under the extract's row lock, so the clearance's postings, the extract's state
    /// and the cheque's state commit in one save. A clearance posts the cheque's cash and withheld amounts and completes
    /// the collection when they reach what is expected; a bounce posts nothing and stores the reason on the extract.
    /// </summary>
    public static async Task<IResult> SettleAsync(
        Guid projectId, Guid id, string? bounceReason, bool bounce, StepServices services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);

        var paid = await (
                from cheque in services.Database.Cheques.AsNoTracking()
                join record in services.Database.CollectionRecords.AsNoTracking() on cheque.CollectionId equals record.Id
                where cheque.Id == id && cheque.ProjectId == projectId
                select new { record.ExtractId })
            .FirstOrDefaultAsync(cancellationToken);
        if (paid is null)
        {
            return ResultExtensions.Problem(CollectionErrors.ChequeNotFound);
        }

        ChequeState target = bounce ? ChequeState.Bounced : ChequeState.Cleared;
        Cheque? moving = null;

        // D-244 item 2: a cheque with no extract paid the released hold, and settles against the handover.
        if (paid.ExtractId is null)
        {
            return await SettleReleasedHoldAsync(projectId, id, bounceReason, target, services, cancellationToken);
        }

        return await ExtractSupport.RunAsync(
            projectId,
            paid.ExtractId.Value,
            needsAccounts: !bounce,
            services,
            async (extract, project, context, step, token) =>
            {
                Guid actor = step.CurrentUser.UserId ?? Guid.Empty;
                DateTimeOffset now = step.Clock.GetUtcNow();

                Cheque cheque = await step.Database.Cheques.FirstAsync(candidate => candidate.Id == id, token);
                CollectionRecord record = await step.Database.CollectionRecords.AsNoTracking()
                    .FirstAsync(candidate => candidate.Id == cheque.CollectionId, token);

                // The cheque's own machine first: a received cheque cannot clear or bounce, a settled one cannot move again.
                if (!Cheque.IsLegalTransition(cheque.State, target))
                {
                    return Result.Failure<IReadOnlyList<Posting>>(CollectionErrors.ChequeIllegalTransition);
                }

                IReadOnlyList<Posting> postings = [];
                if (bounce)
                {
                    Result recorded = extract.RecordChequeBounce(actor, bounceReason, now);
                    if (recorded.IsFailure)
                    {
                        return Result.Failure<IReadOnlyList<Posting>>(recorded.Error);
                    }
                }
                else
                {
                    ExtractCollectionState? state = await ExtractCollections.StateAsync(step.Database, extract, token);
                    if (state is null)
                    {
                        return Result.Failure<IReadOnlyList<Posting>>(CollectionErrors.ChequeIllegalTransition);
                    }

                    Result<PostingSet> cleared = extract.ClearCheque(
                        actor, context!, state.Position, record.CashReceived, record.WithheldAtSource, now);
                    if (cleared.IsFailure)
                    {
                        return Result.Failure<IReadOnlyList<Posting>>(cleared.Error);
                    }

                    postings = [.. cleared.Value.Postings];
                }

                Result moved = cheque.MoveTo(target, actor, now, bounceReason);
                if (moved.IsFailure)
                {
                    return Result.Failure<IReadOnlyList<Posting>>(moved.Error);
                }

                moving = cheque;
                return Result.Success(postings);
            },
            cancellationToken,
            respond: () => Microsoft.AspNetCore.Http.Results.Ok(new Response(moving!.Id, moving.State)));
    }

    /// <summary>
    /// A cheque on the released hold clears or bounces under the project row lock. A clearance posts through
    /// <see cref="ReleasedHoldPayments.ClearCheque"/>, the same rules an extract's clearance uses; a bounce posts nothing
    /// and stores the reason on the cheque.
    /// </summary>
    private static Task<IResult> SettleReleasedHoldAsync(
        Guid projectId, Guid id, string? bounceReason, ChequeState target, StepServices services, CancellationToken cancellationToken)
    {
        Cheque? moving = null;

        return Closure.CollectReleasedHold.Handler.RunAsync(
            projectId,
            services.Database,
            services.Rulings,
            services.Clock,
            async (project, accounts, state, actor, now, token) =>
            {
                Cheque cheque = await services.Database.Cheques.FirstAsync(candidate => candidate.Id == id, token);
                CollectionRecord record = await services.Database.CollectionRecords.AsNoTracking()
                    .FirstAsync(candidate => candidate.Id == cheque.CollectionId, token);

                if (!Cheque.IsLegalTransition(cheque.State, target))
                {
                    return Result.Failure<IReadOnlyList<Posting>>(CollectionErrors.ChequeIllegalTransition);
                }

                IReadOnlyList<Posting> postings = [];
                if (target == ChequeState.Cleared)
                {
                    Result<PostingSet> cleared = ReleasedHoldPayments.ClearCheque(
                        project, services.Rulings, accounts, state.Position, record.CashReceived, record.WithheldAtSource,
                        SiteTime.DateOf(now), actor, now);
                    if (cleared.IsFailure)
                    {
                        return Result.Failure<IReadOnlyList<Posting>>(cleared.Error);
                    }

                    postings = [.. cleared.Value.Postings];
                }

                Result moved = cheque.MoveTo(target, actor, now, bounceReason);
                if (moved.IsFailure)
                {
                    return Result.Failure<IReadOnlyList<Posting>>(moved.Error);
                }

                moving = cheque;
                return Result.Success(postings);
            },
            services.CurrentUser,
            respond: () => Microsoft.AspNetCore.Http.Results.Ok(new Response(moving!.Id, moving.State)),
            cancellationToken);
    }
}
