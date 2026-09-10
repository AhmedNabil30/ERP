namespace Kaff.Api.Features.Babs.ListBabs;

/// <summary>One باب, as a row in the flat list.</summary>
/// <param name="Id">The باب.</param>
/// <param name="Code">As stored — trimmed, upper-invariant.</param>
/// <param name="NameAr">As stored — trimmed.</param>
/// <param name="NameEn">As stored — trimmed.</param>
/// <param name="ParentBabId">The parent باب, or <c>null</c> for a root. The client nests the tree; this endpoint does not.</param>
/// <param name="DefaultMarkup">The rate as a fraction — 15% is <c>0.15</c> (D-044 ruling 6).</param>
/// <param name="IsActive">Active or archived.</param>
public sealed record BabSummary(
    Guid Id,
    string Code,
    string NameAr,
    string NameEn,
    Guid? ParentBabId,
    decimal DefaultMarkup,
    bool IsActive);

/// <summary>The أبواب, flat. Empty when none exist — never null.</summary>
/// <param name="Items">
/// Ordered by <c>SortOrder</c>, then by <c>Code</c> — the same باب ordering <c>ListCatalogueItems</c>
/// already relies on for <c>AC-203-I</c>.
/// </param>
public sealed record Response(IReadOnlyList<BabSummary> Items);
