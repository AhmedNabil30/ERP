using Kaff.Domain.Authorization;
using Kaff.Domain.Identity;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Infrastructure.Authorization;

/// <summary>
/// Answers the assignment half of "permission = role × assignment" (spec.md §9).
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing here trusts the token.</b> The <see cref="PermissionSubject"/> handed in has already
/// been read from the database by <see cref="IPermissionSubjectReader"/> — a deactivated or deleted
/// account never reaches this method at all, and the role is the stored one. This class used to
/// perform that check itself, which worked but put it on the wrong side of the boundary: it only ran
/// when a request named a project, so company-wide permissions were never revalidated. See
/// decisions.md D-048.
/// </para>
/// <para>
/// Three ways in, and only three:
/// </para>
/// <list type="bullet">
/// <item><b>Owner</b> — globally scoped. Karim, 2026-08-17: "owner role is like the admin so yes
/// global." The only exception to spec.md §9's assignment rule, and it is reach only: the Owner still
/// holds nothing beyond what the permission catalogue grants. See decisions.md D-010.</item>
/// <item><b>HR</b> — globally scoped, for the same practical reason. Karim, 2026-08-20: "HR does not
/// need to be assigned to a project first in order to staff it." Somebody has to make the first
/// assignment on a new project, and requiring an assignment to create assignments is circular. Reach
/// only, and the reason that is safe is that the grant set is small and touches no money — pinned by
/// <c>Hr_holds_no_permission_that_touches_money</c> (renamed 2026-09-03, SM-33, from
/// <c>Hr_holds_exactly_three_permissions_and_none_touches_money</c> — KAFF-105b's
/// <see cref="Kaff.Domain.Authorization.Permission.ProjectTeamRead"/> made the old, count-shaped name
/// false), which asserts both halves, the exact set and that none of it touches money. This sentence
/// enumerated the set until 2026-08-22 and was one row out of date within two days of being written;
/// the test cannot be. See decisions.md D-044 and D-055 §2.</item>
/// <item><b>Staff</b> — an active <see cref="ProjectAssignment"/> row (spec.md §9).</item>
/// <item><b>A portal client</b> — the project's client is their client (spec.md §12). Compared by
/// identifier against the database, never against anything the request carried, because spec.md §12
/// is absolute that a client "MUST NEVER see … any other client's data".</item>
/// </list>
/// </remarks>
public sealed class ProjectAccessPolicy : IProjectAccessPolicy
{
    private readonly KaffDbContext _context;

    public ProjectAccessPolicy(KaffDbContext context) => _context = context;

    public async Task<ProjectAccess> EvaluateAsync(
        PermissionSubject subject,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subject);

        return subject.Role switch
        {
            Role.Owner => await GlobalReachAsync(
                projectId, ProjectAccessPath.OwnerGlobal, AssignmentLevel.Supervisor, cancellationToken),
            Role.Hr => await GlobalReachAsync(
                projectId, ProjectAccessPath.HrGlobal, AssignmentLevel.Standard, cancellationToken),
            Role.Client => await ClientAccessAsync(subject.ClientId, projectId, cancellationToken),
            _ => await AssignedAccessAsync(subject.UserId, projectId, cancellationToken),
        };
    }

    /// <summary>
    /// Reach without an assignment row, bounded by the project existing.
    /// </summary>
    /// <param name="level">
    /// The seniority the caller is treated as holding. The two callers pass different values, on
    /// purpose.
    /// <list type="bullet">
    /// <item><b>Owner — <see cref="AssignmentLevel.Supervisor"/>.</b> The evaluator compares the
    /// level against each grant's minimum. <c>Standard</c> would silently refuse the Owner the first
    /// time anyone wrote a grant carrying a minimum level, and the failure would read as a permission
    /// bug rather than a level bug.</item>
    /// <item><b>HR — <see cref="AssignmentLevel.Standard"/>.</b> The opposite trade, because the
    /// risks are not symmetric. HR holds nothing that carries a minimum level, so <c>Standard</c>
    /// costs nothing today; if a levelled HR grant is ever written, the failure is a refusal, which
    /// is safe and visible. Granting HR supervisor seniority pre-emptively would make that same
    /// future grant silently succeed, which is neither.</item>
    /// </list>
    /// </param>
    /// <param name="path">
    /// Which of the two global rules this call is. The Owner's reach and HR's used to be
    /// indistinguishable downstream because one method served both, and they are separate rulings
    /// with separate justifications (decisions.md D-010 and D-044 ruling 3). The record of an access
    /// has to be able to say which one ran. See decisions.md D-055 §7.
    /// </param>
    private async Task<ProjectAccess> GlobalReachAsync(
        Guid projectId,
        ProjectAccessPath path,
        AssignmentLevel level,
        CancellationToken cancellationToken)
    {
        bool projectExists = await _context.Projects
            .AnyAsync(project => project.Id == projectId, cancellationToken);

        return projectExists
            ? new ProjectAccess(path, level)
            : ProjectAccess.Denied;
    }

    private async Task<ProjectAccess> ClientAccessAsync(
        Guid? clientId,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        if (clientId is null)
        {
            return ProjectAccess.Denied;
        }

        bool belongsToClient = await _context.Projects
            .AnyAsync(project => project.Id == projectId && project.ClientId == clientId, cancellationToken);

        return belongsToClient
            ? new ProjectAccess(ProjectAccessPath.PortalClient, AssignmentLevel.Standard)
            : ProjectAccess.Denied;
    }

    private async Task<ProjectAccess> AssignedAccessAsync(
        Guid userId,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        AssignmentLevel? level = await _context.ProjectAssignments
            .Where(assignment =>
                assignment.ProjectId == projectId
                && assignment.UserId == userId
                && assignment.RevokedAt == null)
            .Select(assignment => (AssignmentLevel?)assignment.Level)
            .FirstOrDefaultAsync(cancellationToken);

        return level is null
            ? ProjectAccess.Denied
            : new ProjectAccess(ProjectAccessPath.Assignment, level.Value);
    }
}
