using Kaff.Domain.Common;

namespace Kaff.Domain.Boqs;

/// <summary>
/// One BOQ line. Every catalogue value on it is a <b>copy</b> taken when the line was added (spec.md
/// §4.4): there is no catalogue item id and no باب id here, so a later catalogue edit has nothing to
/// follow. The item code and the باب code are plain text kept for display and traceability.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three quantity states, all retained</b> (spec.md §4.3): estimated by Sales, surveyed (حصر) by the
/// Technical Office, executed from the site. The executed quantity is the only column the database lets
/// change after the BOQ is frozen (<c>003_boq_freeze.sql</c>), because it is the one thing execution
/// writes. It is a running total maintained by the daily log slice with additive updates.
/// </para>
/// <para>
/// <b>Not modelled here</b>: Cost Plus classification (supervised / exempt / non-billable, spec.md §5.2)
/// and the Technical Office review queue for custom lines beyond the <see cref="PendingCatalogueReview"/>
/// flag.
/// </para>
/// </remarks>
public sealed class BoqLine : Entity
{
    public const int MaxCodeLength = 40;
    public const int MaxDescriptionLength = 500;
    public const int MaxUnitLength = 32;

    private BoqLine()
    {
    }

    internal BoqLine(
        Guid boqId,
        Guid projectId,
        int position,
        string? itemCode,
        string descriptionAr,
        string? descriptionEn,
        string unit,
        string babCode,
        string babNameAr,
        string babNameEn,
        Money costPrice,
        Money baseSellRate,
        Percentage conditionsRate,
        Percentage lineMarkup,
        decimal? quantityEstimated,
        decimal? quantitySurveyed,
        bool isCustom)
        : base(NewId())
    {
        BoqId = boqId;
        ProjectId = projectId;
        Position = position;
        ItemCode = itemCode;
        DescriptionAr = descriptionAr;
        DescriptionEn = descriptionEn;
        Unit = unit;
        BabCode = babCode;
        BabNameAr = babNameAr;
        BabNameEn = babNameEn;
        CostPrice = costPrice;
        BaseSellRate = baseSellRate;
        ConditionsRate = conditionsRate;
        LineMarkup = lineMarkup;
        QuantityEstimated = quantityEstimated;
        QuantitySurveyed = quantitySurveyed;
        QuantityExecuted = 0m;
        IsCustom = isCustom;
        PendingCatalogueReview = isCustom;
        SellRate = Price(baseSellRate, conditionsRate, lineMarkup);
    }

    public Guid BoqId { get; private set; }

    /// <summary>The project, repeated on the line so the audit trail files a line under its project.</summary>
    public Guid ProjectId { get; private set; }

    /// <summary>Order within the BOQ.</summary>
    public int Position { get; private set; }

    /// <summary>Copied catalogue code. Null on a custom line that was given none.</summary>
    public string? ItemCode { get; private set; }

    public string DescriptionAr { get; private set; } = null!;

    public string? DescriptionEn { get; private set; }

    public string Unit { get; private set; } = null!;

    /// <summary>Copied باب code, name in Arabic and name in English — the line's section.</summary>
    public string BabCode { get; private set; } = null!;

    public string BabNameAr { get; private set; } = null!;

    public string BabNameEn { get; private set; } = null!;

    /// <summary>Copied internal cost. Never in a client-facing output (spec.md §4.2).</summary>
    public Money CostPrice { get; private set; }

    /// <summary>Copied base sell rate before conditions and markup (spec.md §4.2).</summary>
    public Money BaseSellRate { get; private set; }

    /// <summary>The sum of this project's conditions for the line; additive, never compounding (spec.md §4.2).</summary>
    public Percentage ConditionsRate { get; private set; }

    /// <summary>Defaults from the باب at the time the line was added; overridable per line (spec.md §4.2).</summary>
    public Percentage LineMarkup { get; private set; }

    /// <summary><c>baseSellRate × (1 + conditions) × (1 + markup)</c>, computed when written and stored with the line.</summary>
    public Money SellRate { get; private set; }

    public decimal? QuantityEstimated { get; private set; }

    public decimal? QuantitySurveyed { get; private set; }

    public decimal QuantityExecuted { get; private set; }

    public bool IsCustom { get; private set; }

    /// <summary>spec.md §4.5: a custom line lands in a Technical Office queue and never writes the catalogue.</summary>
    public bool PendingCatalogueReview { get; private set; }

    /// <summary>spec.md §4.2.</summary>
    public static Money Price(Money baseSellRate, Percentage conditionsRate, Percentage lineMarkup) =>
        new(baseSellRate.Amount * (1m + conditionsRate.Fraction) * (1m + lineMarkup.Fraction));

    /// <summary>The quantity of the given state, or null if it has not been entered.</summary>
    public decimal? QuantityFor(Rulings.BoqQuantityBasis basis) => basis switch
    {
        Rulings.BoqQuantityBasis.Estimated => QuantityEstimated,
        Rulings.BoqQuantityBasis.Surveyed => QuantitySurveyed,
        _ => throw new InvalidOperationException($"Unknown quantity basis {basis}."),
    };

    internal void SetQuantities(decimal? estimated, decimal? surveyed)
    {
        QuantityEstimated = estimated;
        QuantitySurveyed = surveyed;
    }

    internal void SetPricing(Percentage conditionsRate, Percentage lineMarkup)
    {
        ConditionsRate = conditionsRate;
        LineMarkup = lineMarkup;
        SellRate = Price(BaseSellRate, conditionsRate, lineMarkup);
    }

    internal void MoveTo(int position) => Position = position;
}
