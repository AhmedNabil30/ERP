namespace Kaff.Api.Features.Closure.RejectHoldRelease;

/// <summary>A rejection always carries a reason; a blank one is refused (CLAUDE.md).</summary>
/// <param name="Reason">Why the release is rejected. Stored on the request and the audit trail.</param>
public sealed record Request(string? Reason);
