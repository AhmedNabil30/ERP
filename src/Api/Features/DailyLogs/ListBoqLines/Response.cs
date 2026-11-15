namespace Kaff.Api.Features.DailyLogs.ListBoqLines;

/// <summary>
/// The BOQ lines a Site Engineer records a quantity against. <b>No member carries a rate, cost, amount or total</b>
/// (spec.md section 4.2, AC-600-J): this is a separate read, not <c>BoqLineResponse</c> with columns hidden.
/// </summary>
public sealed record Response(Guid BoqId, IReadOnlyList<QuantityLine> Lines);

public sealed record QuantityLine(
    Guid Id,
    int Position,
    string? ItemCode,
    string DescriptionAr,
    string? DescriptionEn,
    string Unit,
    decimal? QuantityEstimated,
    decimal? QuantitySurveyed,
    decimal QuantityExecuted,
    string BabName);
