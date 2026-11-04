namespace Kaff.Api.Features.Extracts.ReverseCollection;

/// <summary>Reversing a collection always carries a reason; a blank one is refused (CLAUDE.md, KAFF-316 AC-316-F).</summary>
/// <param name="Reason">Why the collection was mistaken. Stored on the gate record and the audit trail.</param>
public sealed record Request(string? Reason);
