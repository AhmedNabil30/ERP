using System.Security.Claims;
using Kaff.Domain.Authorization;
using Kaff.Domain.Identity;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Projects.GetProjectTeam;

/// <summary>How a person came to be on the project (D-227, D-228), derived from the stored assignment row.</summary>
public enum TeamMemberOrigin
{
    /// <summary>Finance, staffed by the system when the project was created or converted.</summary>
    Automatic,

    /// <summary>The person who created the project put himself on it.</summary>
    Creator,

    /// <summary>Somebody added him, at or after creation.</summary>
    Added,
}

/// <summary>
/// One current member. <c>AssignmentId</c> is what the revoke route takes; <c>UserId</c> is never shown. No contact data, nothing financial.
/// <c>CanRemove</c> is true only for a caller who may manage the team and a member the rule allows to be removed.
/// </summary>
public sealed record TeamMember(
    Guid AssignmentId,
    Guid UserId,
    string FullName,
    Role Role,
    AssignmentLevel Level,
    TeamMemberOrigin Origin,
    bool CanRemove,
    DateTimeOffset AssignedAt);

/// <summary>The team tab: <c>CanManage</c> is whether the caller holds the permission the assign and revoke routes require.</summary>
public sealed record TeamResponse(bool CanManage, IReadOnlyList<TeamMember> Members);

internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        KaffDbContext database,
        IAuthorizationService authorization,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        bool canManage = await CallerPermissions.HoldsAsync(authorization, user, Permission.ProjectAssignmentManage);

        DateTimeOffset createdAt = await database.Projects
            .Where(project => project.Id == projectId)
            .Select(project => project.CreatedAt)
            .SingleAsync(cancellationToken);

        var rows = await database.ProjectAssignments
            .AsNoTracking()
            .Where(assignment => assignment.ProjectId == projectId && assignment.RevokedAt == null)
            .Join(database.Users, assignment => assignment.UserId, member => member.Id,
                (assignment, member) => new { assignment, member.FullName, member.Role })
            .ToListAsync(cancellationToken);

        List<TeamMember> members = [.. rows
            .OrderBy(row => row.FullName, StringComparer.CurrentCulture)
            .Select(row => new TeamMember(
                row.assignment.Id,
                row.assignment.UserId,
                row.FullName,
                row.Role,
                row.assignment.Level,
                OriginOf(row.assignment, row.Role, createdAt),
                canManage && ProjectAssignment.MayBeRemoved(row.Role),
                row.assignment.AssignedAt))];

        return Microsoft.AspNetCore.Http.Results.Ok(new TeamResponse(canManage, members));
    }

    /// <summary>
    /// Finance rows written in the save that created the project carry the project's own creation instant (create and convert
    /// both stamp one clock reading on the project and its staffing); the creator's row names him as its own assigner; anything
    /// else was added by a person. ponytail: a row the creator's own save wrote for a Finance creator reads Automatic, which is
    /// also right; upgrade to a stored origin column only if this derivation is ever outgrown.
    /// </summary>
    private static TeamMemberOrigin OriginOf(ProjectAssignment assignment, Role role, DateTimeOffset projectCreatedAt)
        => role == Role.Finance && assignment.AssignedAt == projectCreatedAt ? TeamMemberOrigin.Automatic
            : assignment.AssignedByUserId == assignment.UserId && assignment.AssignedAt == projectCreatedAt ? TeamMemberOrigin.Creator
            : TeamMemberOrigin.Added;
}
