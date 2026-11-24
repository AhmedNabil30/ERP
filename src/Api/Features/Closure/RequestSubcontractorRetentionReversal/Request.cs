namespace Kaff.Api.Features.Closure.RequestSubcontractorRetentionReversal;

/// <summary>Asking for a reversal always carries a reason; a blank one is refused (CLAUDE.md).</summary>
/// <param name="PostingId">The posted retention release to reverse.</param>
/// <param name="Reason">Why the release was a mistake. Stored on the request and the audit trail.</param>
public sealed record Request(Guid PostingId, string? Reason);
