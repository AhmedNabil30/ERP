using Kaff.Domain.Common;

namespace Kaff.Api.Features.Boqs.UpdateBoqLine;

/// <summary>
/// The writable part of a draft BOQ line: its estimated and surveyed quantities and the two price
/// multipliers. Catalogue values (text, unit, cost, base rate) are copies and are not editable here;
/// remove the line and add it again to copy fresh values.
/// </summary>
/// <param name="QuantityEstimated">Entered by Sales.</param>
/// <param name="QuantitySurveyed">Entered by the Technical Office at حصر.</param>
/// <param name="ConditionsRate">Σ of this project's conditions for the line.</param>
/// <param name="LineMarkup">The markup for this line.</param>
public sealed record Request(
    decimal? QuantityEstimated,
    decimal? QuantitySurveyed,
    Percentage ConditionsRate,
    Percentage LineMarkup);
