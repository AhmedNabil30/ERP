namespace Kaff.Api.Features.BoqBases;

/// <summary>One line of a base: a catalogue item and the quantity of it.</summary>
public sealed record BoqBaseLineRequest(Guid CatalogueItemId, decimal Quantity);
