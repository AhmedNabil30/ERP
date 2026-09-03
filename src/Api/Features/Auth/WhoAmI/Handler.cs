using Kaff.Api.Authorization;
using Kaff.Domain.Authorization;
using Kaff.Domain.Identity;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Auth.WhoAmI;

/// <summary>
/// Reports the caller's own identity, the company-wide permissions their role and department hold
/// today, and every project they reach. KAFF-105a, KAFF-105b.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every field is read fresh from <c>Users</c>, never from the token's claims.</b>
/// <c>StaffSessionMinter.ClaimsFor</c> issues only the user id, the display name, the role at mint time
/// and the security stamp — department is not a claim at all. A role changed after the token was minted
/// (KAFF-109, decisions.md D-051 Q27) does not rotate the stamp, so the claim would still read the old
/// role. This is the one endpoint the frontend trusts to say who it is talking to (decisions.md D-050),
/// so answering from the claim would be the exact defect the brief for this story named. The row this
/// handler projects is the one <see cref="LiveSession"/> loaded and checked for this request.
/// </para>
/// <para>
/// <b>The freshness checks are <see cref="LiveSession.RequireLiveSession"/>'s, not this handler's —
/// and that is the fix, not a tidy-up.</b> They lived here as a hand-copy of what
/// <c>PermissionSubjectReader</c> and <c>PermissionEvaluator</c> do together on a gated route, and the
/// copy carried two of the three: <see cref="User.IsActive"/> and the stamp, but not the role bar. So
/// this endpoint answered a <see cref="Role.Subcontractor"/> — spec.md §9, "record only, no login" —
/// with a <c>200</c> and their name (qa/slice-1/verification-2026-08-26.md, <c>V-26-B</c>). All three
/// now live in one place that every exempt route shares. The bar is here because no staff session may
/// exist for that role at all, which is a property of the door — not because a path to it is open
/// today; see decisions.md D-089 on why the report's "reachable in production" is one step longer than
/// recorded, and D-082 §4 on what arguing from reachability costs.
/// </para>
/// <para>
/// <b>Not gated by <c>MustChangePassword</c>, on purpose — <c>AC-105a-C</c>.</b> The endpoint carries no
/// <c>RequirePermission</c>, so <c>PermissionEvaluator</c>'s <c>PasswordChangeRequired</c> short-circuit
/// (D-086) never runs on this route at all; a caller who has not yet replaced a temporary credential
/// still gets a <c>200</c> and this profile (decisions.md D-072 §2). Both the company-wide and the
/// per-project permission sets such a caller sees are still empty, because
/// <see cref="PermissionEvaluator.CompanyWidePermissionsHeld"/> and
/// <see cref="PermissionEvaluator.ProjectScopedPermissionsHeld"/> both run the ordinary evaluator per
/// permission and that evaluator refuses everything while the flag is set — an honest answer to "what
/// can you do right now", not a second rule invented here. The project list itself (which projects are
/// reached) is unaffected — <c>MustChangePassword</c> governs capability, not reach.
/// <see cref="LiveSession"/> does not consult the flag either, for the same reason: it applies the
/// three session facts and nothing about what a session may reach.
/// </para>
/// <para>
/// <b>KAFF-105b — HR gets a different type, not a filtered view.</b> <see cref="Role.Hr"/> never
/// populates <see cref="Response.Projects"/> and every other role never populates
/// <see cref="Response.TeamProjects"/> — decided by the role itself, not by which catalogue grants
/// happen to match, because rule 9 ("HR … does not receive the project dashboard's payload under any
/// circumstance") must hold even if a future grant were added to the catalogue by mistake.
/// </para>
/// <para>
/// <b>No audit record.</b> A read changes nothing; CLAUDE.md requires a record on a state change, and
/// this is not one.
/// </para>
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        HttpContext http, KaffDbContext database, CancellationToken cancellationToken)
    {
        User user = LiveSession.Caller(http);

        var subject = new PermissionSubject(
            user.Id,
            user.Role,
            user.Department,
            user.OperationsSubDepartment,
            user.ClientId,
            user.FullName,
            user.MustChangePassword);

        IReadOnlyList<Permission> permissions = PermissionEvaluator.CompanyWidePermissionsHeld(subject);

        IReadOnlyList<ProjectEntry> projects = [];
        IReadOnlyList<TeamProjectEntry> teamProjects = [];

        // Rule 9 — HR never receives the dashboard payload, and the dashboard's caller never receives
        // HR's. Branched on the role directly rather than left to fall out of which catalogue grants
        // happen to match, so the separation holds even if a future catalogue row blurred the two.
        if (user.Role == Role.Hr)
        {
            teamProjects = await TeamProjectsAsync(database, cancellationToken);
        }
        else
        {
            projects = await ProjectsAsync(database, subject, cancellationToken);
        }

        return Results.Ok(new Response(
            user.Id,
            user.FullName,
            user.Role,
            user.Department,
            user.OperationsSubDepartment,
            user.MustChangePassword,
            permissions,
            projects,
            teamProjects));
    }

    /// <summary>
    /// Rules 1, 2, 3, 5, 11, 12 — every project a non-HR staff caller reaches, how they reach it, and
    /// what <see cref="PermissionScope.ProjectScoped"/> permissions they hold there.
    /// </summary>
    /// <remarks>
    /// Queried directly rather than one <see cref="IProjectAccessPolicy"/> call per project: that
    /// policy answers "may this user reach this one project", the question a route with a project id
    /// already asks, and calling it once per row here would be exactly the N+1 shape it exists to
    /// avoid. The Owner's branch mirrors <c>ProjectAccessPolicy.GlobalReachAsync</c>'s own path and
    /// level (<c>OwnerGlobal</c>, <c>AssignmentLevel.Supervisor</c> — rule 5); the assignment branch
    /// mirrors <c>ProjectAccessPolicy.AssignedAccessAsync</c>'s own path and level (<c>Assignment</c>,
    /// the row's own <see cref="AssignmentLevel"/> — rule 1). Revoked rows are excluded by the same
    /// <c>RevokedAt == null</c> filter that policy uses (rule 4, <c>AC-105b-H</c>/<c>AC-105b-I</c>).
    /// </remarks>
    private static async Task<IReadOnlyList<ProjectEntry>> ProjectsAsync(
        KaffDbContext database, PermissionSubject subject, CancellationToken cancellationToken)
    {
        if (subject.Role == Role.Owner)
        {
            var everyProject = await database.Projects
                .Select(project => new { project.Id, project.Name, project.Code })
                .ToListAsync(cancellationToken);

            return
            [
                .. everyProject.Select(project => BuildEntry(
                    subject, project.Id, project.Name, project.Code,
                    ProjectAccessPath.OwnerGlobal, AssignmentLevel.Supervisor)),
            ];
        }

        var assigned = await database.ProjectAssignments
            .Where(assignment => assignment.UserId == subject.UserId && assignment.RevokedAt == null)
            .Join(
                database.Projects,
                assignment => assignment.ProjectId,
                project => project.Id,
                (assignment, project) => new { project.Id, project.Name, project.Code, assignment.Level })
            .ToListAsync(cancellationToken);

        return
        [
            .. assigned.Select(row => BuildEntry(
                subject, row.Id, row.Name, row.Code, ProjectAccessPath.Assignment, row.Level)),
        ];
    }

    private static ProjectEntry BuildEntry(
        PermissionSubject subject,
        Guid projectId,
        string name,
        string code,
        ProjectAccessPath path,
        AssignmentLevel level)
    {
        var access = new ProjectAccess(path, level);

        return new ProjectEntry(
            projectId,
            name,
            code,
            path,
            level,
            PermissionEvaluator.ProjectScopedPermissionsHeld(subject, projectId, access));
    }

    /// <summary>
    /// Rules 6, 6a, 7 — every project that exists, HR's global reach needing no assignment row exactly
    /// as the Owner's does, with its team size as a left-joined count rather than an inner one so a
    /// project with nobody on it still appears, at zero (rule 7, <c>AC-105b-D</c>).
    /// </summary>
    private static async Task<IReadOnlyList<TeamProjectEntry>> TeamProjectsAsync(
        KaffDbContext database, CancellationToken cancellationToken)
    {
        Dictionary<Guid, int> teamSizes = await database.ProjectAssignments
            .Where(assignment => assignment.RevokedAt == null)
            .GroupBy(assignment => assignment.ProjectId)
            .Select(group => new { ProjectId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.ProjectId, group => group.Count, cancellationToken);

        var everyProject = await database.Projects
            .Select(project => new { project.Id, project.Name, project.Code })
            .ToListAsync(cancellationToken);

        return
        [
            .. everyProject.Select(project => new TeamProjectEntry(
                project.Name, project.Code, teamSizes.GetValueOrDefault(project.Id, 0))),
        ];
    }
}
