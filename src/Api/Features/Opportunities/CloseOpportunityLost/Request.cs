namespace Kaff.Api.Features.Opportunities.CloseOpportunityLost;

/// <summary>spec.md §3: a lost opportunity records a reason. Free text; the reason list waits on R3-Q10.</summary>
public sealed record Request(string? Reason);
