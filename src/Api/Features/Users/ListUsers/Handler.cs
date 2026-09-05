using Kaff.Domain.Identity;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Users.ListUsers;

internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        KaffDbContext database,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        // Every user, active and inactive alike. spec.md §9, D-049 ruling 5: "leavers are deactivated,
        // never deleted" — a list that showed only active accounts would hide the only people
        // ReactivateUser can act on, and S-006 draws the inactive chip precisely because they belong
        // on this screen.
        List<UserSummary> users = await database.Users
            .OrderBy(user => user.FullName)
            .Select(user => new UserSummary(
                user.Id,
                user.UserName,
                user.FullName,
                user.PhoneEntered,
                user.Role,
                user.Department,
                user.OperationsSubDepartment,
                user.IsActive,
                // The names a role change or a deactivation would revoke, from the same predicate both
                // handlers revoke on — `UserId == user.Id && RevokedAt == null`. Written as a
                // correlated projection rather than a second query so the two can only disagree by
                // somebody editing this line.
                database.ProjectAssignments
                    .Where(assignment => assignment.UserId == user.Id && assignment.RevokedAt == null)
                    .Join(
                        database.Projects,
                        assignment => assignment.ProjectId,
                        project => project.Id,
                        (_, project) => project.Name)
                    .ToList()))
            .ToListAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(new Response(users));
    }
}
