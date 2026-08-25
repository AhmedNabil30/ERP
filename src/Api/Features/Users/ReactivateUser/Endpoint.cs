using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Api.Common.Validation;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Users.ReactivateUser;

/// <summary>
/// <c>POST /api/users/{userId}/reactivate</c> — the Owner brings a leaver back, with none of the
/// access they used to have. KAFF-112.
/// </summary>
/// <remarks>
/// <para>
/// <b>The permission check is the <c>RequirePermission</c> line below and nowhere else.</b> It names
/// <c>Permission.UserManage</c>, whose catalogue row is <c>CompanyWide</c> and granted to
/// <c>Role.Owner</c> alone [Verified: 2026-08-25 @ <c>PermissionCatalogue.cs</c> -&gt; the
/// <c>Permission.UserManage</c> row] — D-044 ruling 1, KAFF-112 rule 1. Same row <c>DeactivateUser</c>
/// and <c>CreateUser</c> use; reactivation is not a capability of its own. A test fails the build if
/// the line is absent [Verified: 2026-08-25 @ <c>EndpointPermissionCoverageTests.cs</c> -&gt;
/// <c>Every_mapped_endpoint_carries_a_permission_requirement</c>], decisions.md D-067 and D-069.
/// </para>
/// <para>
/// Company-wide, so no scope is declared: the route carries a <i>user</i> id, not a project, and
/// <c>ProjectScope.FromRoute()</c> would find no project and refuse every caller including the Owner —
/// the same reasoning <c>DeactivateUser</c> and <c>MoveUserDepartment</c> already carry.
/// </para>
/// <para>
/// <b>Restores an identity, not an access.</b> KAFF-112's central rule (D-049 ruling 5): the account
/// comes back with the same id and the same role and department, and with <b>zero</b> project
/// assignments — none of the rows <c>DeactivateUser</c>'s handler revoked (KAFF-111) are touched here.
/// Whoever needs the person on a project again does that through <c>AssignUserToProject</c>
/// (KAFF-113), deliberately, with a fresh author. This endpoint does not call it, and must not.
/// </para>
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/users/{userId:guid}/reactivate";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.UserManage)
            .AddEndpointFilter<ValidationFilter<Request>>()
            .WithName("ReactivateUser")
            .WithTags("Users");
    }
}
