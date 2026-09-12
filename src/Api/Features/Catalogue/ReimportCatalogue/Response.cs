using Kaff.Api.Features.Catalogue.ImportCatalogue;

namespace Kaff.Api.Features.Catalogue.ReimportCatalogue;

/// <summary>
/// One new code the file would add. KAFF-201 <c>AC-201-B</c>/<c>AC-201-F</c> — named before anything changes.
/// </summary>
public sealed record PlannedCreate(
    string Code, string DescriptionAr, string? DescriptionEn, string Unit, string BabCode,
    decimal CostPrice, decimal BaseSellRate);

/// <summary>
/// One existing code the file would re-price, old and new named together — <c>AC-201-B</c>/<c>AC-201-F</c>.
/// </summary>
public sealed record PlannedReprice(
    string Code,
    decimal OldCostPrice, decimal OldBaseSellRate,
    decimal NewCostPrice, decimal NewBaseSellRate);

/// <summary>
/// KAFF-201 — what a second import would do, before it does it. Nothing changes to produce this.
/// </summary>
/// <param name="WillCreateCount">How many new codes the file adds.</param>
/// <param name="WillAffectCount">How many existing codes the file re-prices.</param>
public sealed record PreviewResponse(
    int WillCreateCount,
    int WillAffectCount,
    IReadOnlyList<PlannedCreate> Creates,
    IReadOnlyList<PlannedReprice> Reprices,
    IReadOnlyList<RowFailure> Failures);

/// <summary>KAFF-201 — what a confirmed second import actually did.</summary>
public sealed record ConfirmResponse(
    int CreatedCount,
    int RepricedCount,
    IReadOnlyList<RowFailure> Failures);
