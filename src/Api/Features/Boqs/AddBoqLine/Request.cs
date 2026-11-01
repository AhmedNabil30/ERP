using Kaff.Domain.Common;

namespace Kaff.Api.Features.Boqs.AddBoqLine;

/// <summary>
/// Adds a line to a project's BOQ, opening the BOQ on the first line. Name a catalogue item to copy its
/// values, or give the fields of a custom item (spec.md §4.5) — never both.
/// </summary>
/// <param name="CatalogueItemId">Copy this item's code, text, unit, cost and base sell rate. Its باب gives the default markup.</param>
/// <param name="ItemCode">Custom item only. Optional.</param>
/// <param name="DescriptionAr">Custom item only. Required.</param>
/// <param name="DescriptionEn">Custom item only. Optional.</param>
/// <param name="Unit">Custom item only. Required.</param>
/// <param name="BabId">Custom item only. The section the line sits in; its default markup applies.</param>
/// <param name="CostPrice">Custom item only. Internal cost, never client-facing.</param>
/// <param name="BaseSellRate">Custom item only.</param>
/// <param name="QuantityEstimated">Entered by Sales (spec.md §4.3).</param>
/// <param name="QuantitySurveyed">Entered by the Technical Office at حصر (spec.md §4.3).</param>
/// <param name="ConditionsRate">Σ of this project's conditions for the line. Additive. Defaults to zero.</param>
/// <param name="LineMarkup">Overrides the باب default markup for this line.</param>
public sealed record Request(
    Guid? CatalogueItemId,
    string? ItemCode,
    string? DescriptionAr,
    string? DescriptionEn,
    string? Unit,
    Guid? BabId,
    Money? CostPrice,
    Money? BaseSellRate,
    decimal? QuantityEstimated,
    decimal? QuantitySurveyed,
    Percentage? ConditionsRate,
    Percentage? LineMarkup);
