using Kaff.Domain.Identity;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Projects.GetProjectTeam;

/// <summary>One active assignment, with the person's name.</summary>
public sealed record TeamMember(Guid UserId, string FullName, Role Role, AssignmentLevel Level, DateTimeOffset AssignedAt);

internal static class Handler
{
    public static async Task<IResult> HandleAsync(Guid projectId, KaffDbContext database, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        List<TeamMember> team = await database.ProjectAssignments
            .AsNoTracking()
            .Where(assignment => assignment.ProjectId == projectId && assignment.RevokedAt == null)
            .Join(database.Users, assignment => assignment.UserId, user => user.Id,
                (assignment, user) => new TeamMember(user.Id, user.FullName, user.Role, assignment.Level, assignment.AssignedAt))
            .ToListAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(team.OrderBy(member => member.FullName, StringComparer.CurrentCulture).ToList());
    }
}
