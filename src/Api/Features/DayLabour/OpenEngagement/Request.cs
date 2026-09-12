namespace Kaff.Api.Features.DayLabour.OpenEngagement;

/// <summary>
/// Opens one engagement for a worker on the route's project. KAFF-210.
/// </summary>
/// <remarks>
/// <b>No money member.</b> decisions.md D-140 leaves open whether a Site Engineer may see or record
/// the agreed day rate on this shared route (Q76, unanswered). Until Nabil rules it, this request
/// carries no rate and the handler opens every engagement with <c>DayRate: null</c>.
/// </remarks>
/// <param name="WorkerId">The day labourer being engaged — company-wide pool, decisions.md D-140 point 3.</param>
public sealed record Request(Guid? WorkerId);
