using Kaff.Domain.Authorization;
using Kaff.Domain.Identity;

namespace Kaff.Api.Features.Auth.WhoAmI;

/// <summary>
/// The caller's own identity and the company-wide permissions their role holds today. KAFF-105a rule 1.
/// </summary>
/// <param name="UserId">The signed-in user's id.</param>
/// <param name="DisplayName">
/// Read fresh from <c>Users</c>, never from the token — the token does not even carry a name claim
/// current beyond mint time for anything but display (decisions.md D-075's discipline, applied here).
/// </param>
/// <param name="Role">The role the database holds now, not the role the token was minted with (KAFF-109).</param>
/// <param name="Department">Null for the Owner and for every external role (spec.md §9).</param>
/// <param name="OperationsSubDepartment">Set only inside <see cref="Identity.Department.Operations"/>.</param>
/// <param name="MustChangePassword">
/// KAFF-105a rule 3 / <c>AC-105a-C</c>, decisions.md D-072 §2 — a field on a <c>200</c>, never a
/// refusal. The SPA routes to the mandatory change screen on this flag; the server does not refuse the
/// call for it.
/// </param>
/// <param name="Permissions">
/// <see cref="PermissionScope.CompanyWide"/> rows only (rule 4). <see cref="Permission.PortalRead"/>
/// and <see cref="Permission.PortalApprove"/> are <see cref="PermissionScope.ProjectScoped"/> and belong
/// to the per-project list, KAFF-105b — not here, and not for any role, including <see cref="Identity.Role.Client"/>.
/// </param>
/// <param name="Projects">
/// KAFF-105b rules 1-5, 7, 11, 12. Every project the caller reaches through the staff dashboard route —
/// an active assignment, or the Owner's global reach (rule 5) — with how they reach it and what they
/// may do there. <b>Empty for <see cref="Identity.Role.Hr"/></b>, whose entries are
/// <see cref="TeamProjects"/> instead: two distinct CLR types by construction (<c>AC-105b-F</c>), not
/// one type filtered, because a filtered view leaks the first time somebody adds a field (D-051, the
/// same argument spec.md §12 makes for the client portal).
/// </param>
/// <param name="TeamProjects">
/// KAFF-105b rules 6, 6a, 7, 8, 9 — <see cref="Identity.Role.Hr"/>'s entries, and
/// <see cref="Identity.Role.Hr"/>'s alone; empty for every other role. Carries exactly
/// <see cref="TeamProjectEntry.Name"/>, <see cref="TeamProjectEntry.Code"/> and
/// <see cref="TeamProjectEntry.TeamSize"/> — never <see cref="Permission.ProjectRead"/>'s payload,
/// never a financial field, and never the pre-formatted <c>"[RefCode] Project Name"</c> string, which
/// D-100 fixes as a display concern for the rendering stories (KAFF-115, KAFF-113, KAFF-125) rather
/// than a value this server computes.
/// </param>
public sealed record Response(
    Guid UserId,
    string DisplayName,
    Role Role,
    Department? Department,
    OperationsSubDepartment? OperationsSubDepartment,
    bool MustChangePassword,
    IReadOnlyList<Permission> Permissions,
    IReadOnlyList<ProjectEntry> Projects,
    IReadOnlyList<TeamProjectEntry> TeamProjects);

/// <summary>
/// One project a staff caller reaches through the ordinary project dashboard route. KAFF-105b rules 1-5.
/// </summary>
/// <param name="ProjectId">Needed to open the project; HR's <see cref="TeamProjectEntry"/> carries no
/// equivalent, on purpose — see that type's remarks.</param>
/// <param name="AccessPath">
/// <see cref="ProjectAccessPath.OwnerGlobal"/> or <see cref="ProjectAccessPath.Assignment"/> here —
/// <see cref="ProjectAccessPath.HrGlobal"/> never reaches this type (rule 9) and
/// <see cref="ProjectAccessPath.PortalClient"/> never reaches this endpoint at all (<c>AC-105b-G</c>).
/// Rule 3: "Owner, globally" and "assigned on 3 June" are different facts and must not be merged.
/// </param>
/// <param name="Level">The caller's seniority on this specific project (rule 11) — never flattened.</param>
/// <param name="Permissions">
/// Every <see cref="PermissionScope.ProjectScoped"/> catalogue row this caller holds on this project,
/// computed by <see cref="PermissionEvaluator.ProjectScopedPermissionsHeld"/> — never a hand-written
/// list (rule 2, <c>AC-105b-J</c>).
/// </param>
public sealed record ProjectEntry(
    Guid ProjectId,
    string Name,
    string Code,
    ProjectAccessPath AccessPath,
    AssignmentLevel Level,
    IReadOnlyList<Permission> Permissions);

/// <summary>
/// One project as <see cref="Identity.Role.Hr"/> sees it — the Project Team surface, never the
/// dashboard. KAFF-105b rules 6, 6a, D-051 (Q32), D-100 (Q43).
/// </summary>
/// <remarks>
/// <b>Carries exactly three fields, and no <c>ProjectId</c>.</b> D-100's ruling is that the reference
/// <see cref="Code"/> is "the hard identifier that prevents HR from misallocating staff to the wrong
/// site" — the field the story names as HR's identifier is the code, not the row's internal key.
/// <c>AC-105b-F</c> fixes this type's whole allowed surface as name, code, team size (and, on the
/// dedicated per-project team roster of KAFF-115, per-member name, role and level) — nothing else,
/// ever, and a reflection test fails the instant that changes.
/// </remarks>
/// <param name="TeamSize">
/// The count of active <see cref="Identity.ProjectAssignment"/> rows on this project — D-100 (Q43),
/// the same set rules 1 and 4 already define. Derived on every read, never stored, for the reason
/// CLAUDE.md never stores a balance.
/// </param>
public sealed record TeamProjectEntry(string Name, string Code, int TeamSize);
