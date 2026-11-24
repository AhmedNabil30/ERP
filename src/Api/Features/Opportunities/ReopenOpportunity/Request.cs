namespace Kaff.Api.Features.Opportunities.ReopenOpportunity;

/// <summary>An optional note on why the opportunity is back. spec.md §3 requires none.</summary>
public sealed record Request(string? Reason);
