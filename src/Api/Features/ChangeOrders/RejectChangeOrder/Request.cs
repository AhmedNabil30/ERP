namespace Kaff.Api.Features.ChangeOrders.RejectChangeOrder;

/// <summary>A rejection always carries a reason; a blank one is refused (spec.md section 13, CLAUDE.md).</summary>
/// <param name="Reason">Why the change order is rejected. Stored on the gate record and the audit trail.</param>
public sealed record Request(string? Reason);
