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
public sealed record Response(
    Guid UserId,
    string DisplayName,
    Role Role,
    Department? Department,
    OperationsSubDepartment? OperationsSubDepartment,
    bool MustChangePassword,
    IReadOnlyList<Permission> Permissions);
