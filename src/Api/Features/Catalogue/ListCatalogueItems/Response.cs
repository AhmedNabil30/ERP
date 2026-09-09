using Kaff.Domain.MasterData;

namespace Kaff.Api.Features.Catalogue.ListCatalogueItems;

/// <summary>
/// One catalogue item, as a row in the search result. KAFF-203.
/// </summary>
/// <remarks>
/// <para>
/// <b>Deliberately the same shape as <c>CreateCatalogueItem.Response</c> and
/// <c>EditCatalogueItem.Response</c></b>, minus nothing — this is the internal (Technical Office /
/// Owner) surface, not a portal one (rule 5, rule 6). S-017 is <c>TO, O</c> and <c>costPrice</c> is
/// deliberately carried, not an oversight — <c>AC-203-G</c>'s positive control, <c>TC-2-033</c>'s
/// second half.
/// </para>
/// <para>
/// <b>No money beyond the item's own two prices</b> (rule 8, <c>AC-203-G</c>) — no total, no margin,
/// no project or BOQ value joined in. There is none on the entity to project.
/// </para>
/// </remarks>
/// <param name="Id">The item.</param>
/// <param name="Code">As stored — trimmed, upper-invariant.</param>
/// <param name="DescriptionAr">As stored — trimmed.</param>
/// <param name="DescriptionEn">As stored — trimmed, or absent.</param>
/// <param name="Unit">As stored — trimmed.</param>
/// <param name="BabId">The باب this item belongs to.</param>
/// <param name="CostPrice">Internal — §4.2. Present because this whole surface is internal (rule 5).</param>
/// <param name="BaseSellRate">Before conditions and line markup.</param>
/// <param name="Status">Active or Archived.</param>
public sealed record CatalogueItemSummary(
    Guid Id,
    string Code,
    string DescriptionAr,
    string? DescriptionEn,
    string Unit,
    Guid BabId,
    decimal CostPrice,
    decimal BaseSellRate,
    CatalogueItemStatus Status);

/// <summary>
/// The catalogue items that matched. KAFF-203.
/// </summary>
/// <param name="Items">
/// Ordered by باب, then by item code within each باب (rule 12, D-129 §5, <c>AC-203-I</c>). The
/// Arabic-collation question — whether ا / أ / إ sort together — is not decided here; it is the
/// Architect's per <c>Q63</c>'s standing caveat. Empty when nothing matched — never null, so a screen
/// can render an honest empty state (<c>AC-203-D</c>).
/// </param>
public sealed record Response(IReadOnlyList<CatalogueItemSummary> Items);
