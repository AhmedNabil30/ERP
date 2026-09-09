namespace Kaff.Api.Features.Catalogue.EditCatalogueItem;

/// <summary>
/// What the Technical Office (or the Owner) sends to correct an item's description, unit or price.
/// KAFF-202.
/// </summary>
/// <remarks>
/// <para>
/// <b>PUT, and the body states the whole editable record.</b> There is no <c>Code</c> member — the
/// entity has no mutator for it, so a code in the body would bind to nothing (the same structural
/// settlement <c>EditClient.Request</c> uses for its own generated code, decisions.md D-107 §4). There
/// is no <c>BabId</c> member either: moving an item to another باب is <c>KAFF-205</c>, not this story
/// (rule 9's second sentence).
/// </para>
/// <para>
/// <b>Re-pricing here never reaches a signed BOQ or an open estimate.</b> §4.4 is a MUST, and
/// <c>CatalogueItem.Reprice</c> is the only place either price moves — see that method's own remarks.
/// </para>
/// </remarks>
/// <param name="DescriptionAr">Required — the same guard <c>Create</c> applies.</param>
/// <param name="DescriptionEn">Optional.</param>
/// <param name="Unit">Required.</param>
/// <param name="CostPrice">Internal — §4.2. Not negative (rule 8); a loss-making sell rate is not refused.</param>
/// <param name="BaseSellRate">Before conditions and line markup.</param>
public sealed record Request(
    string? DescriptionAr,
    string? DescriptionEn,
    string? Unit,
    decimal CostPrice,
    decimal BaseSellRate);
