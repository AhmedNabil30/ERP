namespace Kaff.Api.Features.Babs.EditBab;

/// <summary>
/// What the Technical Office (or the Owner) sends to correct a باب's names or its default markup.
/// KAFF-204.
/// </summary>
/// <remarks>
/// <b>No <c>Code</c> and no <c>ParentBabId</c>.</b> The code has no mutator, the same structural
/// settlement <c>EditCatalogueItem.Request</c> uses. Re-parenting is <c>KAFF-205</c>'s own endpoint,
/// which runs through <c>Bab.SetParent</c>'s cycle guard — a plain edit must not be a second, narrower
/// path into that guard.
/// </remarks>
/// <param name="NameAr">Required.</param>
/// <param name="NameEn">Required.</param>
/// <param name="DefaultMarkup">
/// Required — KAFF-204 rule 2. Per decisions.md D-135, a bare <c>decimal</c> carried as a JSON string
/// on the wire, and it is <b>the fraction</b>: 15% is <c>"0.15"</c>.
/// </param>
public sealed record Request(string? NameAr, string? NameEn, decimal? DefaultMarkup);
