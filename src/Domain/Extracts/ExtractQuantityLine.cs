using Kaff.Domain.Boqs;
using Kaff.Domain.Common;

namespace Kaff.Domain.Extracts;

/// <summary>
/// One BOQ line's quantity as an extract certified it (D-248): the cumulative executed quantity copied when the extract
/// was prepared, and the same line's figure on the previous extract. Insert-only. A revision of the extract's evidence
/// writes a new <see cref="Take"/> beside the old one, and a read takes the latest.
/// </summary>
public sealed class ExtractQuantityLine : Entity
{
    private ExtractQuantityLine()
    {
    }

    private ExtractQuantityLine(Guid extractId, int take, Guid boqLineId, decimal? previousQuantity, decimal cumulativeQuantity)
        : base(NewId())
    {
        ExtractId = extractId;
        Take = take;
        BoqLineId = boqLineId;
        PreviousQuantity = previousQuantity;
        CumulativeQuantity = cumulativeQuantity;
    }

    public Guid ExtractId { get; private set; }

    /// <summary>1 at prepare, one more at each evidence revision.</summary>
    public int Take { get; private set; }

    public Guid BoqLineId { get; private set; }

    /// <summary>The line on the previous extract. Null when that extract was prepared before quantities were kept.</summary>
    public decimal? PreviousQuantity { get; private set; }

    public decimal CumulativeQuantity { get; private set; }

    /// <summary>The quantity of this period: cumulative less previous, or null when the previous is not known.</summary>
    public decimal? CurrentQuantity => CumulativeQuantity - PreviousQuantity;

    /// <summary>Copies each line's executed quantity as it stands now.</summary>
    /// <param name="previousOf">The line's quantity on the previous extract, or null when it is not known.</param>
    public static IReadOnlyList<ExtractQuantityLine> Capture(
        Guid extractId, int take, IEnumerable<BoqLine> lines, Func<Guid, decimal?> previousOf)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(previousOf);

        return [.. lines.Select(line => new ExtractQuantityLine(extractId, take, line.Id, previousOf(line.Id), line.QuantityExecuted))];
    }
}
