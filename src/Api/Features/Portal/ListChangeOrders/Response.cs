namespace Kaff.Api.Features.Portal.ListChangeOrders;

/// <summary>One priced line of a change order, as the client is asked to approve it.</summary>
public sealed record PortalChangeOrderLine(int LineNumber, string Description, string Unit, decimal Quantity, string UnitPrice, string Total);

/// <summary>
/// A priced change order from the step where it waits for the client onward (R5-Q3 and Q14 defaults, spec.md sections 12 and 13).
/// <paramref name="CanDecide"/> is true only while it waits at the client's gate. No author, no review history and no internal reason.
/// </summary>
public sealed record PortalChangeOrder(
    Guid Id,
    int Number,
    string Title,
    string Status,
    string Total,
    bool CanDecide,
    IReadOnlyList<PortalChangeOrderLine> Lines);
