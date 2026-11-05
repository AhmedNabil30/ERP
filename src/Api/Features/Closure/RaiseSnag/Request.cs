using Kaff.Domain.Closure;

namespace Kaff.Api.Features.Closure.RaiseSnag;

/// <summary>A snag has all five fields (spec.md section 11); a missing one is refused.</summary>
/// <param name="Description">What is wrong.</param>
/// <param name="Location">Where.</param>
/// <param name="OwnerKind">Kaff or subcontractor.</param>
/// <param name="SubcontractorId">The owning subcontractor; present exactly when the owner is one.</param>
/// <param name="Priority">Minor, or major which blocks handover.</param>
public sealed record Request(
    string? Description, string? Location, SnagOwnerKind? OwnerKind, Guid? SubcontractorId, SnagPriority? Priority);
