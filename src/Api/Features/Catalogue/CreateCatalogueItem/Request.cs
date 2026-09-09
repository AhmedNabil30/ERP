namespace Kaff.Api.Features.Catalogue.CreateCatalogueItem;

/// <summary>
/// What the Technical Office (or the Owner, D-129 §1) sends to add one catalogue item. KAFF-202.
/// </summary>
/// <remarks>
/// <b>The code is typed, not generated.</b> spec.md §4.1 makes the item's code one of its imported
/// fields — the opposite of a client code (KAFF-202 rule 2, contrast with D-049 ruling 7). Uniqueness
/// is <c>ux_catalogue_items_code</c>, not a lookup here (rule 3, <c>AC-202-B</c>).
/// </remarks>
/// <param name="Code">Required. Normalised (trimmed, upper-invariant) by <c>CatalogueItem.Create</c>.</param>
/// <param name="DescriptionAr">Required — spec.md §4.1.</param>
/// <param name="DescriptionEn">Optional.</param>
/// <param name="Unit">Required — the unit of measure, م٢ / م٣ / عدد as the Technical Office writes it.</param>
/// <param name="BabId">Required and must name an existing باب — KAFF-202 rule 9.</param>
/// <param name="CostPrice">
/// Internal. §4.2 — MUST NOT appear in any client-facing output. Carried as a plain <c>decimal</c> on
/// the wire (matching <c>decimal(18,4)</c> storage exactly) rather than as <c>Money</c>, because the
/// HTTP JSON pipeline has no <c>Money</c> converter registered — only <c>KaffJson.Options</c>, used for
/// audit snapshots, does.
/// </param>
/// <param name="BaseSellRate">Before conditions and line markup — spec.md §4.2.</param>
public sealed record Request(
    string? Code,
    string? DescriptionAr,
    string? DescriptionEn,
    string? Unit,
    Guid BabId,
    decimal CostPrice,
    decimal BaseSellRate);
