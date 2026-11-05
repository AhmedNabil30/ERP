namespace Kaff.Api.Features.Adjustments.ReverseAdjustment;

/// <summary>Reversing an approved adjustment always carries a reason; a blank one is refused (CLAUDE.md).</summary>
/// <param name="Reason">Why the adjustment was mistaken. Stored on the adjustment and the audit trail.</param>
public sealed record Request(string? Reason);
