namespace Kaff.Api.Features.Catalogue.MoveCatalogueItem;

/// <summary>The moved item, with its new باب. KAFF-205.</summary>
public sealed record Response(Guid Id, Guid BabId);
