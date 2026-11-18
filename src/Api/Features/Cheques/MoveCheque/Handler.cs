using Kaff.Api.Common.Results;
using Kaff.Api.Features.Extracts;
using Kaff.Domain.Collections;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
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

        Guid? extractId = await (
                from cheque in services.Database.Cheques.AsNoTracking()
                join record in services.Database.CollectionRecords.AsNoTracking() on cheque.CollectionId equals record.Id
                where cheque.Id == id && cheque.ProjectId == projectId
                select (Guid?)record.ExtractId)
            .FirstOrDefaultAsync(cancellationToken);
        if (extractId is null)
        {
            return ResultExtensions.Problem(CollectionErrors.ChequeNotFound);
        }

        ChequeState target = bounce ? ChequeState.Bounced : ChequeState.Cleared;
        Cheque? moving = null;

        return await ExtractSupport.RunAsync(
            projectId,
            extractId.Value,
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

                Result moved = cheque.MoveTo(target, actor, now);
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
}
