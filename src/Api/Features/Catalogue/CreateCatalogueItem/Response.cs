using Kaff.Domain.MasterData;

namespace Kaff.Api.Features.Catalogue.CreateCatalogueItem;

/// <summary>
/// The item, exactly as spec.md §4.1 names it — plus <see cref="Id"/>, needed to reference the row
/// just created. KAFF-202, <c>AC-202-A</c>: "the response carries no field §4.1 does not name".
/// </summary>
/// <remarks>
/// <b>This is the internal (Technical Office / Owner) surface, not a portal one.</b> §4.2 forbids
/// <c>costPrice</c> on client-facing output; this endpoint is neither — S-018 is <c>TO, O</c> and
/// internal, and there is no <c>/api/portal/*</c> route anywhere near it. Carrying the cost price here
/// is deliberate, not an oversight — see <c>AC-202-G</c>'s positive control.
/// </remarks>
/// <param name="Id">The new item.</param>
/// <param name="Code">As typed, normalised.</param>
/// <param name="DescriptionAr">As stored — trimmed.</param>
/// <param name="DescriptionEn">As stored — trimmed, or absent.</param>
/// <param name="Unit">As stored — trimmed.</param>
/// <param name="BabId">The باب this item belongs to.</param>
/// <param name="CostPrice">Internal — §4.2.</param>
/// <param name="BaseSellRate">Before conditions and line markup.</param>
/// <param name="Status">Always <c>Active</c> on creation — <c>CatalogueItem.Create</c> says so.</param>
public sealed record Response(
    Guid Id,
    string Code,
    string DescriptionAr,
    string? DescriptionEn,
    string Unit,
    Guid BabId,
    decimal CostPrice,
    decimal BaseSellRate,
    CatalogueItemStatus Status);
