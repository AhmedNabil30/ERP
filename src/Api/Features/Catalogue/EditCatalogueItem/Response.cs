using Kaff.Domain.MasterData;

namespace Kaff.Api.Features.Catalogue.EditCatalogueItem;

/// <summary>The item as it now stands. KAFF-202.</summary>
/// <remarks>
/// Same shape as <c>CreateCatalogueItem.Response</c>, for the same reason that response states: this
/// is the internal (Technical Office / Owner) surface, not a portal one, so carrying <c>CostPrice</c>
/// is deliberate.
/// </remarks>
/// <param name="Id">The item.</param>
/// <param name="Code">Unchanged, always — this story has no path that moves it.</param>
/// <param name="DescriptionAr">As stored — trimmed.</param>
/// <param name="DescriptionEn">As stored — trimmed, or absent.</param>
/// <param name="Unit">As stored — trimmed.</param>
/// <param name="BabId">Unchanged — moving باب is <c>KAFF-205</c>.</param>
/// <param name="CostPrice">Internal — §4.2.</param>
/// <param name="BaseSellRate">Before conditions and line markup.</param>
/// <param name="Status">Untouched by an edit — archiving is <c>KAFF-206</c>.</param>
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
