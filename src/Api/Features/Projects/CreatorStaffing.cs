using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Projects;

/// <summary>
/// D-227 item 1: whoever opens a project is assigned to it in the same save, beside Finance (D-217 item 36). Shared by
/// <c>CreateProject</c> and <c>ConvertToExecution</c>, the two handlers behind <c>Permission.ProjectCreate</c>.
/// </summary>
/// <remarks>
/// The Owner gets no row: Owner reach is global (<c>ProjectAccessPolicy</c>) and a row would be one the access model never
/// reads. A creator who is already in <paramref name="alreadyStaffed"/> (an active Finance user) gets no second row.
/// The row is tracked, so the audit interceptor writes its <c>Created</c> record in the same save.
/// </remarks>
internal static class CreatorStaffing
{
    public static async Task<Result> StaffAsync(
        KaffDbContext database,
        ICurrentUser currentUser,
        Guid projectId,
        IReadOnlyCollection<Guid> alreadyStaffed,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } creatorId || currentUser.Role == Role.Owner || alreadyStaffed.Contains(creatorId))
        {
            return Result.Success();
        }

        User creator = await database.Users.SingleAsync(candidate => candidate.Id == creatorId, cancellationToken);
        Result<ProjectAssignment> staffed = ProjectAssignment.Create(projectId, creator, AssignmentLevel.Standard, creatorId, now);

        if (staffed.IsSuccess)
        {
            database.ProjectAssignments.Add(staffed.Value);
        }

        return staffed.IsSuccess ? Result.Success() : Result.Failure(staffed.Error);
    }
}
