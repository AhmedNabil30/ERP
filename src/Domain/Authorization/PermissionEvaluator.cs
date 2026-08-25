using Kaff.Domain.Identity;

namespace Kaff.Domain.Authorization;

/// <summary>The caller's live facts, read from the users table on this request.</summary>
/// <param name="FullName">
/// Not consulted by <see cref="PermissionEvaluator"/> — it rides along because the gate's read is the
/// one place in a request that has the actor's own row in hand, and the audit trail needs the name
/// and the role from <b>that</b> row rather than from the token. Fetching it separately would be a
/// second read of a row already loaded. See decisions.md D-075.
/// </param>
public sealed record PermissionSubject(
    Guid UserId,
    Role Role,
    Department? Department,
    OperationsSubDepartment? OperationsSubDepartment,
    Guid? ClientId,
    string FullName);

/// <summary>
/// How a user came to reach a project — or <see cref="None"/>, meaning they did not.
/// </summary>
/// <remarks>
/// <para>
/// Two of these used to be one branch: <see cref="IProjectAccessPolicy"/> routed the Owner and HR
/// through the same global-reach method, so downstream nothing could tell an Owner's reach from
/// HR's. They are different rules with different justifications (decisions.md D-010 and D-044
/// ruling 3) and they must be distinguishable in the record of what happened.
/// </para>
/// <para>
/// <b>This lands before the first consumer on purpose.</b> KAFF-116 records how access was granted,
/// the audit table is append-only and trigger-protected, and a field that was never written cannot
/// be backfilled. Shipping it after the first access-policy consumer would leave a permanent gap.
/// See decisions.md D-055 §7.
/// </para>
/// </remarks>
public enum ProjectAccessPath
{
    /// <summary>No path. The only value a refusal may carry.</summary>
    None = 0,

    /// <summary>The Owner's global reach, without an assignment row. spec.md §9 amendment, D-010.</summary>
    OwnerGlobal = 1,

    /// <summary>HR's global reach, so it can staff a project nobody is assigned to yet. D-044 ruling 3.</summary>
    HrGlobal = 2,

    /// <summary>An active <see cref="ProjectAssignment"/> row — spec.md §9's ordinary case.</summary>
    Assignment = 3,

    /// <summary>
    /// A portal client reaching their own project. spec.md §12 — deliberately not folded into
    /// <see cref="Assignment"/>, because it is not one: no assignment row exists, and the match is
    /// <c>Project.ClientId</c> against <c>User.ClientId</c>. A separate boundary deserves a separate
    /// name in the trail (decisions.md D-035).
    /// </summary>
    PortalClient = 4,
}

/// <summary>The outcome of asking whether a user may reach a project.</summary>
/// <param name="Path">
/// How the reach was granted. <see cref="ProjectAccessPath.None"/> means it was not — so a refusal
/// cannot claim a grant path, and a grant cannot be nameless. <see cref="Granted"/> is derived from
/// this rather than stored beside it, which is what makes the contradiction unrepresentable.
/// </param>
/// <param name="Level">Seniority on the assignment. <see cref="AssignmentLevel.Standard"/> for non-engineers.</param>
public sealed record ProjectAccess(ProjectAccessPath Path, AssignmentLevel Level)
{
    public static readonly ProjectAccess Denied = new(ProjectAccessPath.None, AssignmentLevel.Standard);

    /// <summary>True when the user is assigned, has global reach, or is the project's portal client.</summary>
    public bool Granted => Path != ProjectAccessPath.None;
}

/// <summary>Why a permission was granted or refused. Logged, and mapped to an i18n key by the Api.</summary>
public enum PermissionDecision
{
    Granted = 0,
    NotAuthenticated = 1,

    /// <summary>spec.md §9: "Subcontractor (record only, no login)."</summary>
    RoleCannotLogIn = 2,

    /// <summary>No catalogue entry. Deny by default.</summary>
    UnknownPermission = 3,

    /// <summary>The role, department or sub-department holds no grant for this permission.</summary>
    RoleNotGranted = 4,

    /// <summary>A project-scoped permission was requested without a project in the route.</summary>
    ProjectNotSpecified = 5,

    /// <summary>spec.md §9: role alone is insufficient — the user is not assigned to this project.</summary>
    NotAssignedToProject = 6,

    /// <summary>spec.md §9: a junior may draft but not submit.</summary>
    AssignmentLevelTooLow = 7,
}

/// <summary>
/// The permission rule, as a pure function.
/// </summary>
/// <remarks>
/// <para>
/// spec.md §9: "Permission = role × assignment." Both factors are evaluated here, in one place.
/// </para>
/// <para>
/// <b>This function has no super-user branch, and the Owner's global reach is not in it.</b> Karim's
/// answer of 2026-08-17 — "owner role is like the admin so yes global" — is about *reach*, not
/// capability, and is implemented in <see cref="IProjectAccessPolicy"/>: for an Owner the policy
/// returns access without an assignment row. What arrives here is still a <see cref="ProjectAccess"/>
/// like any other, so an Owner whose access the policy refuses is refused here exactly like anyone
/// else, and an Owner still holds only what <see cref="PermissionCatalogue"/> grants. See
/// decisions.md D-010.
/// </para>
/// <para>
/// Pure and synchronous so it can be exhaustively tested without a database or an HTTP context. The
/// two impure parts — reading the caller's identity, and looking up the assignment — are supplied as
/// arguments by the Api's authorization handler.
/// </para>
/// </remarks>
public static class PermissionEvaluator
{
    public static PermissionDecision Evaluate(
        PermissionSubject? subject,
        Permission permission,
        Guid? projectId,
        ProjectAccess? projectAccess)
    {
        if (subject is null)
        {
            return PermissionDecision.NotAuthenticated;
        }

        // spec.md §9 — a subcontractor is a record, never a login. Refused before the catalogue is
        // consulted, so a grant added to the catalogue by mistake still cannot let one in.
        if (subject.Role == Role.Subcontractor)
        {
            return PermissionDecision.RoleCannotLogIn;
        }

        if (!PermissionCatalogue.TryGet(permission, out PermissionDefinition? definition) || definition is null)
        {
            return PermissionDecision.UnknownPermission;
        }

        return Evaluate(subject, definition, projectId, projectAccess);
    }

    /// <summary>
    /// The same rule against a definition supplied directly, rather than looked up by name.
    /// </summary>
    /// <remarks>
    /// Exists so the department-only refusal below can be tested against a definition that the
    /// shipped catalogue deliberately no longer contains. The overload above is what production
    /// calls; this is the same code path, not a parallel one.
    /// </remarks>
    public static PermissionDecision Evaluate(
        PermissionSubject? subject,
        PermissionDefinition definition,
        Guid? projectId,
        ProjectAccess? projectAccess)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (subject is null)
        {
            return PermissionDecision.NotAuthenticated;
        }

        if (subject.Role == Role.Subcontractor)
        {
            return PermissionDecision.RoleCannotLogIn;
        }

        // A grant that names a department and no role is satisfied by ANY role carrying that
        // department. On anything that moves money that is a hole, not a shortcut: it is how a
        // portal client nearly reached EmployeeManage (D-035), how a Marketing user in the HR
        // department held it (D-044 ruling 2), and how a site engineer came to hold
        // SiteExpenseConfirm — the one role spec.md §8 excludes by name (F-04).
        //
        // The catalogue no longer contains such a grant and a test fails the build if one returns.
        // This refuses it here as well, at the point of decision, because the test only protects the
        // rows that exist today. Architect, 2026-08-21: "Financial permissions … must never be
        // granted to a bare department without specifying a role." See decisions.md D-053.
        List<AccessGrant> matching =
        [
            .. definition.Grants.Where(grant =>
                (!definition.TouchesMoney || grant.Role is not null) && Matches(grant, subject)),
        ];

        if (matching.Count == 0)
        {
            return PermissionDecision.RoleNotGranted;
        }

        if (definition.Scope == PermissionScope.CompanyWide)
        {
            return PermissionDecision.Granted;
        }

        if (projectId is null)
        {
            return PermissionDecision.ProjectNotSpecified;
        }

        if (projectAccess is null || !projectAccess.Granted)
        {
            return PermissionDecision.NotAssignedToProject;
        }

        // spec.md §9 — junior versus supervisor. At least one matching grant must be satisfied by the
        // seniority the user actually holds on this project.
        bool levelSatisfied = matching.Exists(grant => projectAccess.Level >= grant.MinimumAssignmentLevel);

        return levelSatisfied ? PermissionDecision.Granted : PermissionDecision.AssignmentLevelTooLow;
    }

    private static bool Matches(AccessGrant grant, PermissionSubject subject)
    {
        if (grant.Role is not null && grant.Role != subject.Role)
        {
            return false;
        }

        if (grant.Department is not null && grant.Department != subject.Department)
        {
            return false;
        }

        if (grant.OperationsSubDepartment is not null
            && grant.OperationsSubDepartment != subject.OperationsSubDepartment)
        {
            return false;
        }

        return true;
    }
}
