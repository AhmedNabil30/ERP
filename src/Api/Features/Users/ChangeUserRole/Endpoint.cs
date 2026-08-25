using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Users.ChangeUserRole;

/// <summary>
/// <c>PUT /api/users/{userId}/role</c> — the Owner changes someone's role. KAFF-109.
/// </summary>
/// <remarks>
/// <para>
/// <b>The permission check is the <c>RequirePermission</c> line below and nowhere else.</b> It names
/// <c>Permission.UserManage</c>, whose catalogue row is <c>CompanyWide</c> and granted to
/// <c>Role.Owner</c> alone [Verified: 2026-08-25 @ <c>PermissionCatalogue.cs</c> -&gt; the
/// <c>Permission.UserManage</c> row] — D-044 ruling 1, KAFF-109 rule 1. Same row <c>CreateUser</c>,
/// <c>MoveUserDepartment</c>, <c>DeactivateUser</c> and <c>ReactivateUser</c> use: setting a role is
/// not a capability of its own. HR is refused here even though HR holds
/// <c>ProjectAssignmentManage</c> with global reach — staffing a project is not the same act as
/// deciding what role someone holds. A test fails the build if the line below is absent
/// [Verified: 2026-08-25 @ <c>EndpointPermissionCoverageTests.cs</c> -&gt;
/// <c>Every_mapped_endpoint_carries_a_permission_requirement</c>], decisions.md D-067 and D-069.
/// </para>
/// <para>
/// Company-wide, so no scope is declared: the route carries a <i>user</i> id, not a project, and
/// <c>ProjectScope.FromRoute()</c> would find no project and refuse every caller including the Owner —
/// the same reasoning <c>MoveUserDepartment</c>, <c>DeactivateUser</c> and <c>ReactivateUser</c> carry.
/// </para>
/// <para>
/// <b>PUT, not POST.</b> The body states the role and replaces it, the same shape
/// <c>MoveUserDepartment</c> uses for a department — this is a field being set, not an act with no
/// resource of its own. It differs from that endpoint only in what it returns: a role change has a
/// side effect worth reporting (the projects it revoked), so this returns <b>200 OK</b> with a body
/// rather than <b>204 No Content</b>.
/// </para>
/// <para>
/// <b>Whether an Owner may change their own role is not answered by any source cited to this story</b>
/// — spec.md §9's "nobody creates and approves the same movement" governs financial movements, and a
/// role change moves nobody's money. Nothing here refuses a caller acting on their own id; nothing
/// here was asked to. Flagged for Karim rather than decided.
/// </para>
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/users/{userId:guid}/role";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPut(Route, Handler.HandleAsync)
            .RequirePermission(Permission.UserManage)
            .WithName("ChangeUserRole")
            .WithTags("Users");
    }
}
