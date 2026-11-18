using System.Security.Claims;
using Kaff.Domain.Collections;
using Kaff.Domain.Common;
using Kaff.Domain.Extracts;
using Kaff.Domain.Identity;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Kaff.Api.Features.Extracts.CollectExtract;

/// <summary>
/// Records a collection of what arrived (D-235 items 1 and 2). Cash and transfer: <c>Extract.Collect</c> posts the cash
/// and the withheld amount and makes the extract Collected once received plus withheld reach what is expected. Cheque:
/// <c>Extract.AcceptCheque</c> checks the same rules and nothing is posted; the cheque starts Received and becomes cash
/// when it clears. The record (and the cheque) are stored in the same save. Either everything commits or nothing does.
/// No posting is edited or deleted; a mistaken collection is a reversal.
/// </summary>
internal static class Handler
{
    public static Task<IResult> HandleAsync(
        Guid projectId,
        Guid id,
        Request request,
        KaffDbContext database,
        RulingSet rulings,
        TimeProvider clock,
        ICurrentUser currentUser,
        IAuthorizationService authorization,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return ExtractSupport.RunAsync(
            projectId,
            id,
            needsAccounts: true,
            new StepServices(database, rulings, clock, currentUser, authorization, user),
            async (extract, project, context, services, token) =>
            {
                Guid actor = services.CurrentUser.UserId ?? Guid.Empty;
                DateTimeOffset now = services.Clock.GetUtcNow();
                Money cash = request.CashReceived ?? Money.Zero;
                Money withheld = request.WithheldAtSource ?? Money.Zero;

                // Null only before Issued, where EnsureMayRecordCollection refuses anyway.
                ExtractCollectionState? state = await ExtractCollections.StateAsync(services.Database, extract, token);
                CollectionPosition position = state?.Position ?? new CollectionPosition(Money.Zero, Money.Zero, Money.Zero);
                bool creditRaised = state?.CreditNoteRaised ?? false;

                IReadOnlyList<Posting> postings = [];
                if (request.Method == CollectionMethod.Cheque)
                {
                    Result accepted = extract.AcceptCheque(project, services.Rulings, position, creditRaised, cash, withheld);
                    if (accepted.IsFailure)
                    {
                        return Result.Failure<IReadOnlyList<Posting>>(accepted.Error);
                    }
                }
                else
                {
                    Result<PostingSet> collected = extract.Collect(actor, context!, position, cash, withheld, now, creditRaised);
                    if (collected.IsFailure)
                    {
                        return Result.Failure<IReadOnlyList<Posting>>(collected.Error);
                    }

                    postings = [.. collected.Value.Postings];
                }

                Result<CollectionRecord> recorded = CollectionRecord.Record(
                    project.Id, extract.Id, request.Method, request.Date, request.Reference, cash, withheld, actor, now);
                if (recorded.IsFailure)
                {
                    return Result.Failure<IReadOnlyList<Posting>>(recorded.Error);
                }

                services.Database.CollectionRecords.Add(recorded.Value);
                if (recorded.Value.Method == CollectionMethod.Cheque)
                {
                    services.Database.Cheques.Add(Cheque.Receive(recorded.Value, actor, now));
                }

                return Result.Success(postings);
            },
            cancellationToken);
    }
}
