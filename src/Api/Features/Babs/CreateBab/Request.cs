namespace Kaff.Api.Features.Babs.CreateBab;

/// <summary>
/// What the Technical Office (or the Owner, D-129 §1) sends to add one باب. KAFF-204.
/// </summary>
/// <param name="Code">Required. Normalised (trimmed, upper-invariant) by <c>Bab.Create</c>. Unique — <c>ux_babs_code</c>.</param>
/// <param name="NameAr">Required — the product name.</param>
/// <param name="NameEn">Required.</param>
/// <param name="ParentBabId">Optional. When given, must name an existing باب — a root when omitted.</param>
/// <param name="DefaultMarkup">
/// Required (KAFF-204 rule 2 — no null markup, no inheritance). Per decisions.md D-135, carried as a
/// bare <c>decimal</c> that crosses the wire as a JSON string — <b>the fraction</b>, matching
/// <c>ListBabs</c>'s existing response shape (<c>DefaultMarkup.Fraction</c>): 15% is <c>"0.15"</c>,
/// never <c>"15"</c>.
/// </param>
/// <param name="SortOrder">Optional, defaults to 0.</param>
public sealed record Request(
    string? Code,
    string? NameAr,
    string? NameEn,
    Guid? ParentBabId,
    decimal? DefaultMarkup,
    int SortOrder = 0);
