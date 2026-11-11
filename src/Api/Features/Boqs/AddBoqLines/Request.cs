namespace Kaff.Api.Features.Boqs.AddBoqLines;

/// <summary>One catalogue item to add. Quantities are optional here: a line without one is refused at signature (D-217 item 23).</summary>
public sealed record BoqLineItem(Guid CatalogueItemId, decimal? QuantityEstimated, decimal? QuantitySurveyed);

/// <summary>Several catalogue items for one BOQ. All are added or none is.</summary>
public sealed record Request(IReadOnlyList<BoqLineItem>? Items);
