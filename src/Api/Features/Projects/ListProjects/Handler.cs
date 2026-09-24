using Kaff.Api.Common.Results;
using Kaff.Domain.Authorization;
using Kaff.Domain.Identity;
using Kaff.Domain.Projects;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Projects.ListProjects;

/// <summary>
/// Every project the caller may reach. KAFF-400, S-050.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this handler re-reads the subject instead of trusting the gate's yes/no.</b>
/// <c>Permission.ProjectList</c> is <c>CompanyWide</c> so the gate can admit the request at all — a
/// list request cannot name the one project it is about, the same reasoning that made
/// <c>ProjectCreate</c> company-wide (decisions.md D-055 §3). But spec.md §9's "role × assignment"
/// still has to decide <i>which rows</i> come back, and that half is this handler's, not the gate's —
/// exactly the shape <c>WhoAmI.Handler.ProjectsAsync</c> already uses for KAFF-105b's per-user project
/// list. <see cref="IPermissionSubjectReader"/> is the same seam <c>PermissionAuthorizationHandler</c>
/// itself uses, so this is not a second, hand-rolled read of the users table's meaning — it is the one
/// mechanism, called again for the same request.
/// </para>
/// <para>
/// <b>Owner sees every project, mirroring <c>ProjectAccessPolicy.GlobalReachAsync</c>.</b> Every other
/// role that can reach this handler at all — Finance, Technical Office, Marketing, Site Engineer — has
/// no global reach in <c>IProjectAccessPolicy</c>, so their rows come from an active
/// <c>ProjectAssignment</c> join, the same query shape <c>ProjectAccessPolicy.AssignedAccessAsync</c>
/// and <c>WhoAmI.Handler.ProjectsAsync</c> both use.
/// </para>
/// <para>
/// <b>No audit record.</b> A read changes nothing (CLAUDE.md).
/// </para>
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        HttpContext http,
        KaffDbContext database,
        ICurrentUser currentUser,
        IPermissionSubjectReader subjectReader,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(subjectReader);

        // RequirePermission(Permission.ProjectList) has already run: an authenticated subject with a
        // role holding the grant is guaranteed here. The read is repeated rather than passed through
        // HttpContext because PermissionSubject is not stashed anywhere the handler can reach it —
        // ScopedTo/ActorVerifiedAs record the decision for the audit trail, not the subject itself.
        PermissionSubject? subject = currentUser.UserId is { } userId
            ? await subjectReader.ReadAsync(userId, currentUser.SecurityStamp, cancellationToken)
            : null;

        if (subject is null)
        {
            return ResultExtensions.Problem(AuthorizationErrors.Forbidden);
        }

        IQueryable<Project> query = database.Projects;

        if (subject.Role != Role.Owner)
        {
            var assignedProjectIds = database.ProjectAssignments
                .Where(assignment => assignment.UserId == subject.UserId && assignment.RevokedAt == null)
                .Select(assignment => assignment.ProjectId);

            query = query.Where(project => assignedProjectIds.Contains(project.Id));
        }

        // Order the entity query by its own Code column before projecting — EF cannot translate an
        // OrderBy over a member access on a freshly constructed ProjectSummary (it tries to rewrite
        // OrderBy(x => new ProjectSummary(...).Code) and fails to simplify that back to the column).
        List<ProjectSummary> projects = await query
            .OrderBy(project => project.Code)
            .Select(project => new ProjectSummary(
                project.Id, project.Code, project.Name, project.ClientId,
                project.ContractType, project.Status, project.ContractValue))
            .ToListAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(new Response(projects, projects.Count));
    }
}
