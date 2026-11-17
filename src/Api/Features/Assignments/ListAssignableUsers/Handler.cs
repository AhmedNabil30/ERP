using Kaff.Domain.Identity;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Assignments.ListAssignableUsers;

/// <summary>One person the add sheet may offer. <c>LevelRequired</c> is true for a site engineer, who is added as Junior or Supervisor; everyone else is added at the standard level.</summary>
public sealed record Candidate(Guid UserId, string FullName, Role Role, bool LevelRequired);

internal static class Handler
{
    public static async Task<IResult> HandleAsync(Guid projectId, KaffDbContext database, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        // The Owner and HR reach every project without a row; a Client or Subcontractor is refused by ProjectAssignment.Create.
        Role[] never = [Role.Owner, Role.Hr, Role.Client, Role.Subcontractor];

        var people = await database.Users
            .AsNoTracking()
            .Where(user => user.IsActive
                           && !never.Contains(user.Role)
                           && !database.ProjectAssignments.Any(assignment =>
                               assignment.ProjectId == projectId && assignment.UserId == user.Id && assignment.RevokedAt == null))
            .Select(user => new { user.Id, user.FullName, user.Role })
            .ToListAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(people
            .OrderBy(person => person.FullName, StringComparer.CurrentCulture)
            .Select(person => new Candidate(person.Id, person.FullName, person.Role, person.Role == Role.SiteEngineer))
            .ToList());
    }
}
