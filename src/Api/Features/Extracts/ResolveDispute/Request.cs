using Kaff.Domain.Common;
using Kaff.Domain.Extracts;

namespace Kaff.Api.Features.Extracts.ResolveDispute;

/// <summary>
/// Resolves a disputed extract. The reason is stored on the gate record and on the credit note.
/// </summary>
/// <param name="Kind">Credit note or revised extract (D-241 item 2).</param>
/// <param name="Reason">Why the dispute resolves this way. Required.</param>
/// <param name="Amount">The credit note's amount, positive, with no cap (D-239 item 2). Ignored for a revised extract.</param>
public sealed record Request(DisputeResolutionKind? Kind, string? Reason, Money Amount);
