using Kaff.Domain.Common;

namespace Kaff.Domain.MasterData;

/// <summary>
/// Lifecycle of a catalogue item.
/// </summary>
/// <remarks>
/// 🟡 spec.md §4.1 lists <c>status</c> as a field but does not enumerate its values. Active and
/// Archived are the minimum the freeze rule needs. See decisions.md D-018 — this is a question for
/// Nabil, not a decision taken here.
/// </remarks>
public enum CatalogueItemStatus
{
    Active = 1,
    Archived = 2,
}

/// <summary>
/// One catalogue line serving both sales and execution. Owned by the Technical Office (spec.md §2, §4.1).
/// </summary>
/// <remarks>
/// <para>
/// spec.md §4.1: "One catalogue serves both sales and execution. Item: code · description · unit ·
/// bab · costPrice · baseSellRate · status. Loaded from Excel at setup. Edited manually after. Excel
/// import is not an ongoing sync."
/// </para>
/// <para>
/// <b>The freeze rule.</b> spec.md §4.4: at contract signature the BOQ *copies* these values. A
/// signed BOQ line MUST NOT hold a foreign key back to this row, because a later edit here would
/// then reach a signed contract. That rule is enforced where the BOQ is built (slice 4); this entity
/// exists to be copied from.
/// </para>
/// <para>
/// <see cref="CostPrice"/> MUST NOT appear in any client-facing output (spec.md §4.2).
/// </para>
/// </remarks>
public sealed class CatalogueItem : Entity
{
    public const int MaxCodeLength = 40;
    public const int MaxDescriptionLength = 500;
    public const int MaxUnitLength = 32;

    private CatalogueItem()
    {
    }

    private CatalogueItem(
        Guid id,
        string code,
        string descriptionAr,
        string? descriptionEn,
        string unit,
        Guid babId,
        Money costPrice,
        Money baseSellRate)
        : base(id)
    {
        Code = code;
        DescriptionAr = descriptionAr;
        DescriptionEn = descriptionEn;
        Unit = unit;
        BabId = babId;
        CostPrice = costPrice;
        BaseSellRate = baseSellRate;
        Status = CatalogueItemStatus.Active;
    }

    public string Code { get; private set; } = null!;

    public string DescriptionAr { get; private set; } = null!;

    public string? DescriptionEn { get; private set; }

    /// <summary>Unit of measure as written by the Technical Office — م٢, م٣, عدد.</summary>
    public string Unit { get; private set; } = null!;

    public Guid BabId { get; private set; }

    /// <summary>Internal cost. Never shown to a client (spec.md §4.2).</summary>
    public Money CostPrice { get; private set; }

    /// <summary>Base sell rate before conditions and line markup (spec.md §4.2).</summary>
    public Money BaseSellRate { get; private set; }

    public CatalogueItemStatus Status { get; private set; }

    public static Result<CatalogueItem> Create(
        string code,
        string descriptionAr,
        string unit,
        Guid babId,
        Money costPrice,
        Money baseSellRate,
        string? descriptionEn = null)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > MaxCodeLength)
        {
            return Result.Failure<CatalogueItem>(MasterDataErrors.CodeRequired);
        }

        if (string.IsNullOrWhiteSpace(descriptionAr) || descriptionAr.Length > MaxDescriptionLength)
        {
            return Result.Failure<CatalogueItem>(MasterDataErrors.DescriptionRequired);
        }

        if (string.IsNullOrWhiteSpace(unit) || unit.Length > MaxUnitLength)
        {
            return Result.Failure<CatalogueItem>(MasterDataErrors.UnitRequired);
        }

        if (costPrice.IsNegative)
        {
            return Result.Failure<CatalogueItem>(MasterDataErrors.CostPriceMustNotBeNegative);
        }

        if (baseSellRate.IsNegative)
        {
            return Result.Failure<CatalogueItem>(MasterDataErrors.SellRateMustNotBeNegative);
        }

        return Result.Success(new CatalogueItem(
            NewId(),
            code.Trim().ToUpperInvariant(),
            descriptionAr.Trim(),
            string.IsNullOrWhiteSpace(descriptionEn) ? null : descriptionEn.Trim(),
            unit.Trim(),
            babId,
            costPrice,
            baseSellRate));
    }

    /// <summary>
    /// Repricing. Signed BOQs are unaffected — spec.md §4.4 makes them copies with no link to follow.
    /// Open estimates are re-priced only through the explicit review of spec.md §4.4.
    /// </summary>
    public Result Reprice(Money costPrice, Money baseSellRate)
    {
        if (costPrice.IsNegative)
        {
            return Result.Failure(MasterDataErrors.CostPriceMustNotBeNegative);
        }

        if (baseSellRate.IsNegative)
        {
            return Result.Failure(MasterDataErrors.SellRateMustNotBeNegative);
        }

        CostPrice = costPrice;
        BaseSellRate = baseSellRate;
        return Result.Success();
    }

    public Result Archive()
    {
        if (Status == CatalogueItemStatus.Archived)
        {
            return Result.Failure(MasterDataErrors.AlreadyArchived);
        }

        Status = CatalogueItemStatus.Archived;
        return Result.Success();
    }
}
