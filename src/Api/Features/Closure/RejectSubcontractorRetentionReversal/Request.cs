namespace Kaff.Api.Features.Closure.RejectSubcontractorRetentionReversal;

/// <summary>Returning a request always carries a reason; a blank one is refused (CLAUDE.md).</summary>
/// <param name="Reason">Why the reversal is returned to Finance. Stored on the request and the audit trail.</param>
public sealed record Request(string? Reason);
