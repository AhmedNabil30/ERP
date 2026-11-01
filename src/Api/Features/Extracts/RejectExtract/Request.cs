namespace Kaff.Api.Features.Extracts.RejectExtract;

/// <summary>A rejection always carries a reason; a blank one is refused (spec.md section 7, CLAUDE.md).</summary>
/// <param name="Reason">Why the extract goes back to Draft. Stored on the gate record and the audit trail.</param>
public sealed record Request(string? Reason);
