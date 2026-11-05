using Kaff.Domain.Closure;

namespace Kaff.Api.Features.Closure.ResolveSnag;

/// <summary>The dual toggle of spec.md section 11 with its reason. No amount: the cost is unruled (Q112).</summary>
/// <param name="Kind">Debit note or absorbed.</param>
/// <param name="Reason">Why. Required.</param>
public sealed record Request(SnagResolutionKind? Kind, string? Reason);
