using Kaff.Domain.Adjustments;
using Kaff.Domain.Collections;
using Kaff.Domain.Common;
using Kaff.Domain.Extracts;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Extracts;

/// <summary>
/// What an issued extract has against it right now, read from its documents and postings on every request (D-235
/// item 2): the collection position, whether a credit note waits for the Owner, and the cheques still in hand.
/// </summary>
/// <param name="ChequesInHand">The extract's cheques that can still clear or bounce, oldest first.</param>
internal sealed record ExtractCollectionState(
    CollectionPosition Position,
    bool CreditNoteRaised,
    IReadOnlyList<(Cheque Cheque, CollectionRecord Record)> ChequesInHand);

/// <summary>
/// The one place the collection routes, the cheque routes and the extract's read derive the position, so the control a
/// screen shows and the refusal a route gives cannot disagree.
/// </summary>
internal static class ExtractCollections
{
    /// <summary>Null before the extract is issued, when nothing is owed on it yet.</summary>
    public static async Task<ExtractCollectionState?> StateAsync(KaffDbContext database, Extract extract, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(extract);

        if (extract.NetPayable is not { } net || !ExtractMoneyQueries.IsBilled(extract.Status))
        {
            return null;
        }

        // Money is a complex type EF cannot sum in SQL, so the amounts are read and added in memory.
        var credits = await database.Adjustments
            .AsNoTracking()
            .Where(adjustment => adjustment.SourceDocumentType == SourceDocumentType.Extract
                && adjustment.SourceDocumentId == extract.Id
                && (adjustment.Status == AdjustmentStatus.Raised || adjustment.Status == AdjustmentStatus.Approved))
            .Select(adjustment => new { adjustment.Status, adjustment.Amount })
            .ToListAsync(cancellationToken);

        Money approved = Money.Sum(credits.Where(credit => credit.Status == AdjustmentStatus.Approved).Select(credit => credit.Amount));
        Money collected = (await ExtractMoneyQueries.CollectedAsync(database, [extract.Id], cancellationToken))
            .GetValueOrDefault(extract.Id, Money.Zero);

        List<(Cheque Cheque, CollectionRecord Record)> inHand =
        [
            .. (await (
                    from cheque in database.Cheques
                    join record in database.CollectionRecords on cheque.CollectionId equals record.Id
                    where record.ExtractId == extract.Id
                    orderby record.RecordedAt
                    select new { cheque, record })
                .ToListAsync(cancellationToken))
                .Where(row => Cheque.IsInHand(row.cheque.State))
                .Select(row => (row.cheque, row.record)),
        ];

        return new ExtractCollectionState(
            new CollectionPosition(net - approved, collected, Money.Sum(inHand.Select(row => row.Record.Settles))),
            credits.Exists(credit => credit.Status == AdjustmentStatus.Raised),
            inHand);
    }
}
