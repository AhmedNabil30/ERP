namespace Kaff.Api.Features.PettyCash.ReturnRequest;

/// <summary>A return always carries a reason; a blank one is refused (CLAUDE.md).</summary>
/// <param name="Reason">Why the draft goes back to the junior. Stored on the request and the audit trail.</param>
public sealed record Request(string? Reason);
