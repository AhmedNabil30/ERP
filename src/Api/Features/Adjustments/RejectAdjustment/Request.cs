namespace Kaff.Api.Features.Adjustments.RejectAdjustment;

/// <summary>A rejection always carries a reason; a blank one is refused (CLAUDE.md).</summary>
/// <param name="Reason">Why the adjustment is rejected. Stored on the adjustment and the audit trail.</param>
public sealed record Request(string? Reason);
