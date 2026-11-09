using Kaff.Domain.Common;

namespace Kaff.Api.Features.Projects.SignProject;

/// <summary>
/// There is deliberately no contract value here. It is derived at signature from the BOQ, never typed.
/// </summary>
/// <param name="SignedOn">The signature date. Defaults to today when omitted.</param>
/// <param name="HoldPercentage">
/// Lump Sum only. The fraction, e.g. <c>"0.20"</c> for 20% (D-151), the same type as
/// <c>CreateProject.Request</c>. Omitted keeps the project's current hold. KAFF-417.
/// </param>
/// <param name="ClientAdvance">
/// Lump Sum only. Percent units, <c>25</c> means 25%, read as the advance rate; the same unit as
/// <c>CreateProject.Request</c>. Omitted keeps the project's current advance. KAFF-417.
/// </param>
/// <param name="AreaSquareMetres">
/// Design only (KAFF-417 AC-417-K). Decimal string, e.g. <c>"100"</c> (D-135). Required when the project has no area yet;
/// the fee is priced as area x the default rate per m2. Refused on Lump Sum and Cost Plus.
/// </param>
public sealed record Request(
    DateOnly? SignedOn, Percentage? HoldPercentage = null, decimal? ClientAdvance = null, decimal? AreaSquareMetres = null);
