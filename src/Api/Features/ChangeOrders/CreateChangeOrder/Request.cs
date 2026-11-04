using Kaff.Domain.Common;

namespace Kaff.Api.Features.ChangeOrders.CreateChangeOrder;

/// <summary>One priced line. Copied onto the change order; it is never a key into the catalogue or the BOQ.</summary>
/// <param name="Description">What the line is.</param>
/// <param name="Unit">The unit of measure.</param>
/// <param name="Quantity">Strictly positive.</param>
/// <param name="UnitPrice">Strictly positive. The line total is derived and rounded as a BOQ line is.</param>
public sealed record LineRequest(string? Description, string? Unit, decimal Quantity, Money UnitPrice);

/// <summary>
/// A change order. On a Lump Sum project it carries priced lines; on a Cost Plus or Design project it carries none
/// (documentary, unpriced). The price is never typed as a total: it is the sum of the lines.
/// </summary>
/// <param name="Title">What the change order is for.</param>
/// <param name="Lines">The priced lines. Empty on a Cost Plus or Design project.</param>
public sealed record Request(string? Title, IReadOnlyList<LineRequest>? Lines);
