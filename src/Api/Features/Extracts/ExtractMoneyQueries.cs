using Kaff.Domain.Common;
using Kaff.Domain.Extracts;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Extracts;

/// <summary>The money on one extract, derived on every read: the extract's calculated figures and the postings that collected it.</summary>
/// <param name="CertifiedValue">The work value Accounts certified. Null until deductions are applied.</param>
/// <param name="NetPayable">What the client owes on it. Null until deductions are applied.</param>
/// <param name="Collected">Cash received plus tax withheld at source, net of reversals. Null before the extract is issued.</param>
/// <param name="Outstanding">Net payable less collected. Null before the extract is issued.</param>
public sealed record ExtractMoney(Money? CertifiedValue, Money? NetPayable, Money? Collected, Money? Outstanding);

internal static class ExtractMoneyQueries
{
    private static readonly PostingType[] CollectionTypes = [PostingType.ClientCollection, PostingType.TaxWithheldAtSource];

    /// <summary>
    /// What each extract has collected, summed from the collection postings. A reversal subtracts: nothing is
    /// ever read from a balance column. Summed in memory because EF Core cannot translate a sum over a
    /// <see cref="Money"/> complex type.
    /// </summary>
    /// <remarks>
    /// D-242 item 3: what was received on a cancelled extract counts as already paid against the extract that revises it,
    /// so the original's collection postings are read under the revision's id. Nothing is re-posted.
    /// </remarks>
    public static async Task<IReadOnlyDictionary<Guid, Money>> CollectedAsync(
        KaffDbContext database, IReadOnlyCollection<Guid> extractIds, CancellationToken cancellationToken)
    {
        Dictionary<Guid, Guid> carried = await database.Extracts
            .AsNoTracking()
            .Where(extract => extractIds.Contains(extract.Id) && extract.RevisesExtractId != null)
            .ToDictionaryAsync(extract => extract.RevisesExtractId!.Value, extract => extract.Id, cancellationToken);
        Guid[] sources = [.. extractIds, .. carried.Keys];

        var legs = await database.Postings
            .AsNoTracking()
            .Where(posting => posting.SourceDocumentType == SourceDocumentType.Extract
                && sources.Contains(posting.SourceDocumentId)
                && CollectionTypes.Contains(posting.Type))
            .Select(posting => new { posting.SourceDocumentId, posting.Amount, IsReversal = posting.ReversesId != null })
            .ToListAsync(cancellationToken);

        return legs
            .GroupBy(leg => carried.GetValueOrDefault(leg.SourceDocumentId, leg.SourceDocumentId))
            .ToDictionary(
                group => group.Key,
                group => Money.Sum(group.Select(leg => leg.IsReversal ? -leg.Amount : leg.Amount)));
    }

    /// <summary>An extract is billed once it is issued to the client; before that its figures are not yet a debt.</summary>
    public static bool IsBilled(ExtractStatus status) =>
        status is ExtractStatus.Issued or ExtractStatus.Collected or ExtractStatus.Disputed;

    public static ExtractMoney For(Extract extract, IReadOnlyDictionary<Guid, Money> collected)
    {
        ArgumentNullException.ThrowIfNull(extract);
        ArgumentNullException.ThrowIfNull(collected);

        if (!IsBilled(extract.Status) || extract.NetPayable is not { } net)
        {
            return new ExtractMoney(extract.CertifiedValue, extract.NetPayable, null, null);
        }

        Money received = collected.GetValueOrDefault(extract.Id, Money.Zero);
        return new ExtractMoney(extract.CertifiedValue, net, received, net - received);
    }
}
