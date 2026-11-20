using Kaff.Domain.Boqs;
using Kaff.Domain.Common;
using Kaff.Domain.Extracts;
using Kaff.Domain.Rulings;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Extracts;

/// <summary>
/// One BOQ line of an extract in the standard Egyptian layout: previous, current and cumulative quantity (D-248).
/// <paramref name="PreviousQuantity"/> and <paramref name="CurrentQuantity"/> are null when the previous extract was
/// prepared before quantities were kept. The rate and the amounts are null for a viewer who may not see extract money.
/// The باب is the BOQ line's own copy, frozen with it, so a screen can band the rows by باب.
/// </summary>
public sealed record ExtractQuantityRow(
    Guid BoqLineId,
    string? ItemCode,
    string DescriptionAr,
    string? DescriptionEn,
    string Unit,
    decimal? PreviousQuantity,
    decimal? CurrentQuantity,
    decimal CumulativeQuantity,
    Money? Rate,
    Money? PreviousAmount,
    Money? CurrentAmount,
    Money? CumulativeAmount,
    string BabCode,
    string BabNameAr,
    string BabNameEn);

/// <summary>
/// The quantities an extract keeps (D-248). Prepare, the evidence revision and the preview all ask the same previous,
/// so the rows they write or show cannot disagree with each other or with the extract's previously certified work.
/// </summary>
internal static class ExtractQuantities
{
    /// <summary>The latest take of an extract's kept quantities. Empty for an extract prepared before they were kept.</summary>
    public static async Task<List<ExtractQuantityLine>> LatestAsync(KaffDbContext database, Guid extractId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        // ponytail: reads every take to pick the last; there is one take per evidence revision, so a handful at most.
        List<ExtractQuantityLine> lines = await database.ExtractQuantityLines.AsNoTracking()
            .Where(line => line.ExtractId == extractId)
            .ToListAsync(cancellationToken);
        int take = lines.Count == 0 ? 0 : lines.Max(line => line.Take);
        return [.. lines.Where(line => line.Take == take)];
    }

    /// <summary>
    /// Each line's quantity on the extract a new one is measured from: the last extract that stands, as prepare takes
    /// its previously certified work, or for a revision the original's own previous (D-248). Null per line when that
    /// extract kept no quantities.
    /// </summary>
    public static async Task<Func<Guid, decimal?>> PreviousOfAsync(
        KaffDbContext database, Guid projectId, Guid? revisesExtractId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        if (revisesExtractId is { } originalId)
        {
            return Lookup(await LatestAsync(database, originalId, cancellationToken), line => line.PreviousQuantity);
        }

        Guid? previousId = await database.Extracts.AsNoTracking()
            .Where(extract => extract.ProjectId == projectId && extract.Status != ExtractStatus.Void)
            .OrderByDescending(extract => extract.Number)
            .Select(extract => (Guid?)extract.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return previousId is null
            ? _ => 0m
            : Lookup(await LatestAsync(database, previousId.Value, cancellationToken), line => line.CumulativeQuantity);
    }

    /// <summary>A line the kept take does not carry was not on the BOQ then, so its previous is zero.</summary>
    public static Func<Guid, decimal?> Lookup(IReadOnlyCollection<ExtractQuantityLine> kept, Func<ExtractQuantityLine, decimal?> quantity)
    {
        ArgumentNullException.ThrowIfNull(kept);

        if (kept.Count == 0)
        {
            return _ => null;
        }

        Dictionary<Guid, decimal?> byLine = kept.ToDictionary(line => line.BoqLineId, quantity);
        return boqLineId => byLine.TryGetValue(boqLineId, out decimal? value) ? value : 0m;
    }

    /// <summary>
    /// One row, its money at the frozen BOQ rate and rounded as the ruled line total rounds: the same derivation as the
    /// extract's cumulative certified work, so the rows foot to it.
    /// </summary>
    public static ExtractQuantityRow Row(BoqLine line, decimal? previous, decimal cumulative, bool seesMoney, RulingSet rulings)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(rulings);

        Money cumulativeAmount = rulings.ContractValueSource.LineTotal(line.SellRate, cumulative);
        Money? previousAmount = previous is { } known ? rulings.ContractValueSource.LineTotal(line.SellRate, known) : null;
        return new ExtractQuantityRow(
            line.Id, line.ItemCode, line.DescriptionAr, line.DescriptionEn, line.Unit,
            previous, cumulative - previous, cumulative,
            seesMoney ? line.SellRate : null,
            seesMoney ? previousAmount : null,
            seesMoney ? cumulativeAmount - previousAmount : null,
            seesMoney ? cumulativeAmount : null,
            line.BabCode, line.BabNameAr, line.BabNameEn);
    }
}
