namespace Kaff.Api.Features.Catalogue.MoveCatalogueItem;

/// <summary>Moves one catalogue item to a different باب. KAFF-205.</summary>
/// <param name="BabId">The new باب. Required — an item's باب is never nullable (KAFF-205 rule 4).</param>
public sealed record Request(Guid BabId);
