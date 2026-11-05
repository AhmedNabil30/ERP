using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Adjustments.ReverseAdjustment;

/// <summary>
/// Corrects an approved adjustment: each standing posting it wrote is mirrored by a new posting referencing the original
/// through <c>ReversesId</c>. Nothing is updated or deleted.
/// </summary>
internal static class Handler
{
    public static Task<IResult> HandleAsync(
        Guid projectId,
        Guid id,
        Request request,
        KaffDbContext database,
        TimeProvider clock,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return AdjustmentSupport.RunAsync(
            projectId,
            id,
            database,
            async (adjustment, _, token) =>
            {
                List<Posting> postings = await database.Postings
                    .Where(posting => posting.SourceDocumentType == SourceDocumentType.Adjustment
                        && posting.SourceDocumentId == adjustment.Id
                        && posting.ReversesId == null
                        && !database.Postings.Any(other => other.ReversesId == posting.Id))
                    .ToListAsync(token);

                Guid[] accountIds = [.. postings.SelectMany(posting => new[] { posting.FromAccountId, posting.ToAccountId }).Distinct()];
                Dictionary<Guid, Account> accounts = await database.Accounts
                    .Where(account => accountIds.Contains(account.Id))
                    .ToDictionaryAsync(account => account.Id, token);

                IReadOnlyCollection<Guid> alsoActed = await AdjustmentSupport.DisputeActorsAsync(database, adjustment, token);
                return adjustment.Reverse(
                    currentUser.UserId ?? Guid.Empty,
                    alsoActed,
                    request.Reason,
                    [.. postings.Select(posting => (posting, accounts[posting.FromAccountId], accounts[posting.ToAccountId]))],
                    clock.GetUtcNow(),
                    AdjustmentSupport.Today(clock));
            },
            cancellationToken);
    }
}
