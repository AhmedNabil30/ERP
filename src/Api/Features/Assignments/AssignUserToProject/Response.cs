using Kaff.Domain.Identity;

namespace Kaff.Api.Features.Assignments.AssignUserToProject;

/// <summary>The row that was created, as S-010 needs to see it after the sheet closes.</summary>
/// <param name="Id">The assignment's identifier — what KAFF-114 revokes.</param>
/// <param name="ProjectId">From the route.</param>
/// <param name="UserId">The person now on the team.</param>
/// <param name="Level">The seniority this row carries, which is not necessarily the one any other row for the same person carries.</param>
/// <param name="AssignedByUserId">The Owner or the HR user who did it (KAFF-113 rule 10).</param>
/// <param name="AssignedAt">When.</param>
public sealed record Response(
    Guid Id,
    Guid ProjectId,
    Guid UserId,
    AssignmentLevel Level,
    Guid AssignedByUserId,
    DateTimeOffset AssignedAt);
