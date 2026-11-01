using Kaff.Domain.Boqs;
using Kaff.Domain.Common;
using Kaff.Domain.Rulings;

namespace Kaff.Api.Features.Boqs;

/// <summary>
/// One BOQ line as the Technical Office sees it. Includes <c>CostPrice</c>: this projection is for
/// <c>ProjectManage</c> holders and must never be reused for a client-facing or site-facing output
/// (spec.md §4.2).
/// </summary>
/// <param name="LineTotal">Sell rate × the ruled quantity, rounded as ruled. Null while that quantity is not entered.</param>
public sealed record BoqLineResponse(
    Guid Id,
    int Position,
    string? ItemCode,
    string DescriptionAr,
    string? DescriptionEn,
    string Unit,
    string BabCode,
    string BabNameAr,
    string BabNameEn,
    Money CostPrice,
    Money BaseSellRate,
    Percentage ConditionsRate,
    Percentage LineMarkup,
    Money SellRate,
    decimal? QuantityEstimated,
    decimal? QuantitySurveyed,
    decimal QuantityExecuted,
    Money? LineTotal,
    bool IsCustom,
    bool PendingCatalogueReview)
{
    public static BoqLineResponse From(BoqLine line, ContractValueSource rule)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(rule);

        decimal? quantity = line.QuantityFor(rule.QuantityBasis);

        return new BoqLineResponse(
            line.Id,
            line.Position,
            line.ItemCode,
            line.DescriptionAr,
            line.DescriptionEn,
            line.Unit,
            line.BabCode,
            line.BabNameAr,
            line.BabNameEn,
            line.CostPrice,
            line.BaseSellRate,
            line.ConditionsRate,
            line.LineMarkup,
            line.SellRate,
            line.QuantityEstimated,
            line.QuantitySurveyed,
            line.QuantityExecuted,
            quantity is null ? null : rule.LineTotal(line.SellRate, quantity.Value),
            line.IsCustom,
            line.PendingCatalogueReview);
    }
}
