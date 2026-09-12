using Kaff.Domain.Common;

namespace Kaff.Api.Features.Babs.CreateBab;

/// <summary>
/// What the Technical Office (or the Owner, D-129 §1) sends to add one باب. KAFF-204.
/// </summary>
/// <param name="Code">Required. Normalised (trimmed, upper-invariant) by <c>Bab.Create</c>. Unique — <c>ux_babs_code</c>.</param>
/// <param name="NameAr">Required — the product name.</param>
/// <param name="NameEn">Required.</param>
/// <param name="ParentBabId">Optional. When given, must name an existing باب — a root when omitted.</param>
/// <param name="DefaultMarkup">
/// Required (KAFF-204 rule 2 — no null markup, no inheritance). Carried as <see cref="Percentage"/>,
/// which crosses the wire as the fraction (decisions.md D-135, D-151): 15% is <c>"0.15"</c>, never
/// <c>"15"</c>.
/// </param>
/// <param name="SortOrder">Optional, defaults to 0.</param>
public sealed record Request(
    string? Code,
    string? NameAr,
    string? NameEn,
    Guid? ParentBabId,
    Percentage? DefaultMarkup,
    int SortOrder = 0);
