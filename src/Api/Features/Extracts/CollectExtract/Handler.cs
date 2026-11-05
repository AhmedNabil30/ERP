using System.Security.Claims;
using Kaff.Domain.Adjustments;
using Kaff.Domain.Collections;
using Kaff.Domain.Common;
using Kaff.Domain.Extracts;
using Kaff.Domain.Identity;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Extracts.CollectExtract;

/// <summary>
/// Records the collection: <c>Extract.Collect</c> builds the cash and withholding postings and moves the extract
/// to Collected, and the collection (plus a cheque in Received, for a cheque) is stored in the same save. Either
/// everything commits or nothing does. No posting is edited or deleted; a mistaken collection is a reversal.
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
                // KAFF-511/512: a collection posts the stored net, so it would over-collect once a credit note is
                // Raised or Approved against the extract. Refused until Q106 rules how the reduced amount is paid.
                bool creditOpen = await services.Database.Adjustments.AnyAsync(
                    adjustment => adjustment.SourceDocumentType == SourceDocumentType.Extract
                        && adjustment.SourceDocumentId == extract.Id
                        && (adjustment.Status == AdjustmentStatus.Raised || adjustment.Status == AdjustmentStatus.Approved),
                    token);
                if (creditOpen)
                {
                    return Result.Failure<IReadOnlyList<Posting>>(ExtractErrors.CreditNoteOpen);
                }

                Guid actor = services.CurrentUser.UserId ?? Guid.Empty;
                DateTimeOffset now = services.Clock.GetUtcNow();

                Result<PostingSet> collected = extract.Collect(actor, context!, now);
                if (collected.IsFailure)
                {
                    return Result.Failure<IReadOnlyList<Posting>>(collected.Error);
                }

                IReadOnlyList<Posting> postings = [.. collected.Value.Postings];
                Money cash = Money.Sum(postings.Where(p => p.Type == PostingType.ClientCollection).Select(p => p.Amount));
                Money withheld = Money.Sum(postings.Where(p => p.Type == PostingType.TaxWithheldAtSource).Select(p => p.Amount));

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
