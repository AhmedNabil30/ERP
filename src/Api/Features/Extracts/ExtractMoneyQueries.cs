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
/// <param name="InCheques">Cheques received or deposited and not yet cleared, as the extract's own collection position reads them. Null before the extract is issued.</param>
/// <param name="Remaining">What may still be recorded: the extract's collection position's remaining. Null before the extract is issued.</param>
public sealed record ExtractMoney(
    Money? CertifiedValue, Money? NetPayable, Money? Collected, Money? Outstanding, Money? InCheques = null, Money? Remaining = null);

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
        KaffDbContext database, IReadOnlyCollection<Guid> extractIds, CancellationToken cancellationToken) =>
        (await ReceivedAndWithheldAsync(database, extractIds, cancellationToken))
            .ToDictionary(pair => pair.Key, pair => pair.Value.Received + pair.Value.Withheld);

    /// <summary>
    /// <see cref="CollectedAsync"/> split into its two parts: the cash received and the tax withheld at source, each net
    /// of reversals, with the original's payments read under the revision's id (D-242 item 3).
    /// </summary>
    public static async Task<IReadOnlyDictionary<Guid, (Money Received, Money Withheld)>> ReceivedAndWithheldAsync(
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
            .Select(posting => new { posting.SourceDocumentId, posting.Type, posting.Amount, IsReversal = posting.ReversesId != null })
            .ToListAsync(cancellationToken);

        return legs
            .GroupBy(leg => carried.GetValueOrDefault(leg.SourceDocumentId, leg.SourceDocumentId))
            .ToDictionary(
                group => group.Key,
                group => (
                    Money.Sum(group.Where(leg => leg.Type == PostingType.ClientCollection).Select(leg => leg.IsReversal ? -leg.Amount : leg.Amount)),
                    Money.Sum(group.Where(leg => leg.Type == PostingType.TaxWithheldAtSource).Select(leg => leg.IsReversal ? -leg.Amount : leg.Amount))));
    }

    /// <summary>
    /// D-244 item 1: for each revision among <paramref name="extracts"/>, the hold accrued by the cancelled extracts it
    /// replaces, through every link of the chain. A hold accrual is never reversed (the database refuses it), so the
    /// accruals are summed as they stand. Revisions only: a normal extract has no entry.
    /// </summary>
    public static async Task<IReadOnlyDictionary<Guid, Money>> CarriedHoldAsync(
        KaffDbContext database, IReadOnlyCollection<Extract> extracts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(extracts);

        if (!extracts.Any(extract => extract.RevisesExtractId is not null))
        {
            return new Dictionary<Guid, Money>();
        }

        Guid[] projects = [.. extracts.Select(extract => extract.ProjectId).Distinct()];
        Dictionary<Guid, Guid?> revises = await database.Extracts
            .AsNoTracking()
            .Where(extract => projects.Contains(extract.ProjectId))
            .ToDictionaryAsync(extract => extract.Id, extract => extract.RevisesExtractId, cancellationToken);

        Dictionary<Guid, List<Guid>> ancestors = [];
        foreach (Extract extract in extracts.Where(extract => extract.RevisesExtractId is not null))
        {
            List<Guid> chain = [];
            for (Guid? link = extract.RevisesExtractId; link is { } id && !chain.Contains(id); link = revises.GetValueOrDefault(id))
            {
                chain.Add(id);
            }

            ancestors[extract.Id] = chain;
        }

        Guid[] sources = [.. ancestors.Values.SelectMany(chain => chain).Distinct()];
        var accruals = await database.Postings
            .AsNoTracking()
            .Where(posting => posting.SourceDocumentType == SourceDocumentType.Extract
                && sources.Contains(posting.SourceDocumentId)
                && posting.Type == PostingType.HoldAccrual)
            .Select(posting => new { posting.SourceDocumentId, posting.Amount, IsReversal = posting.ReversesId != null })
            .ToListAsync(cancellationToken);

        return ancestors.ToDictionary(
            pair => pair.Key,
            pair => Money.Sum(accruals
                .Where(accrual => pair.Value.Contains(accrual.SourceDocumentId))
                .Select(accrual => accrual.IsReversal ? -accrual.Amount : accrual.Amount)));
    }

    /// <summary>An extract is billed once it is issued to the client; before that its figures are not yet a debt.</summary>
    public static bool IsBilled(ExtractStatus status) =>
        status is ExtractStatus.Issued or ExtractStatus.Collected or ExtractStatus.Disputed;

    /// <param name="carriedHold">What <see cref="CarriedHoldAsync"/> returned for these extracts: a revision owes less by the carried hold above its own.</param>
    public static ExtractMoney For(
        Extract extract, IReadOnlyDictionary<Guid, Money> collected, IReadOnlyDictionary<Guid, Money>? carriedHold = null)
    {
        ArgumentNullException.ThrowIfNull(extract);
        ArgumentNullException.ThrowIfNull(collected);

        if (!IsBilled(extract.Status) || extract.NetPayable is not { } net)
        {
            return new ExtractMoney(extract.CertifiedValue, extract.NetPayable, null, null);
        }

        Money received = collected.GetValueOrDefault(extract.Id, Money.Zero);
        Money held = extract.HeldBeyondOwnHold(carriedHold?.GetValueOrDefault(extract.Id, Money.Zero) ?? Money.Zero);
        return new ExtractMoney(extract.CertifiedValue, net, received, net - held - received);
    }
}
