using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Projects;

/// <summary>
/// Who is on a new project the moment it exists (D-217 item 36, D-227 item 1): every active Finance user, and the person who
/// opened it. Shared by <c>CreateProject</c> and <c>ConvertToExecution</c>, so an execution project made by conversion is staffed
/// exactly like one made by hand. The rows are tracked, so the audit interceptor writes their <c>Created</c> records in the same save.
/// </summary>
internal static class NewProjectStaffing
{
    public static async Task<Result> StaffAsync(
        KaffDbContext database,
        ICurrentUser currentUser,
        Guid projectId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        List<User> financeUsers = await database.Users
            .Where(candidate => candidate.Role == Role.Finance && candidate.IsActive)
            .ToListAsync(cancellationToken);

        foreach (User finance in financeUsers)
        {
            Result<ProjectAssignment> staffed = ProjectAssignment.Create(
                projectId, finance, AssignmentLevel.Standard, currentUser.UserId ?? Guid.Empty, now);

            if (staffed.IsFailure)
            {
                return Result.Failure(staffed.Error);
            }

            database.ProjectAssignments.Add(staffed.Value);
        }

        return await CreatorStaffing.StaffAsync(
            database, currentUser, projectId, financeUsers.ConvertAll(finance => finance.Id), now, cancellationToken);
    }
}
