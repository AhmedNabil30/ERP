namespace Kaff.Api.Features.Extracts.RaiseDispute;

/// <summary>A dispute always carries a reason; a blank one is refused (CLAUDE.md, KAFF-511).</summary>
/// <param name="Reason">Why the client disputes the extract. Stored on the gate record and the audit trail.</param>
public sealed record Request(string? Reason);
