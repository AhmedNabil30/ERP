using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Users.MoveUserDepartment;

/// <summary>
/// <c>PUT /api/users/{userId}/department</c> — the Owner moves someone between departments. KAFF-108.
/// </summary>
/// <remarks>
/// <para>
/// <b>The permission check is the <c>RequirePermission</c> line below and nowhere else</b>
/// (<c>AC-108-E</c>). It names <c>Permission.UserManage</c>, whose catalogue row is
/// <c>CompanyWide</c> and granted to <c>Role.Owner</c> alone
/// [Verified: 2026-08-23 @ <c>PermissionCatalogue.cs</c> -&gt; the <c>Permission.UserManage</c> row]
/// — Karim, 2026-08-20, decisions.md D-044 ruling 1. No catalogue row is added by this story: setting
/// a department is <c>UserManage</c>, which is why KAFF-108 rule 1 cites D-044 ruling 1 rather than
/// naming a permission of its own.
/// </para>
/// <para>
/// Both halves of "role × assignment" are decided from that row, exactly as in
/// <c>Features/Users/CreateUser</c>: the role half against the grants, and the assignment half by
/// the scope, which is company-wide and therefore names no project to be assigned to. The route
/// carries a <i>user</i> id, not a project id, so <c>ProjectScope.FromRoute()</c> would find nothing
/// and refuse every caller including the Owner.
/// </para>
/// <para>
/// <b>The subject's authority is not cached anywhere</b> (<c>AC-108-A</c>, <c>AC-108-B</c>,
/// decisions.md D-048). The move writes two columns and nothing else: no claim is re-issued, no token
/// is minted, no cache is invalidated, because the gate re-reads role, department and sub-department
/// from the database on every authorized request
/// [Verified: 2026-08-23 @ <c>PermissionAuthorizationHandler.cs</c> -&gt; <c>BuildSubjectAsync</c>;
/// @ <c>PermissionSubjectReader.cs</c> -&gt; <c>ReadAsync</c>]. The moved user's existing token keeps
/// working and carries different authority on its next request — which is the behaviour, not a side
/// effect of one.
/// </para>
/// <para>
/// <b>PUT, not PATCH.</b> The body states the department and the sub-department together and replaces
/// both; there is no partial form of this act, because "Operations with no sub-department" is a
/// refusal rather than an omission (<c>AC-108-C</c>).
/// </para>
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/users/{userId:guid}/department";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPut(Route, Handler.HandleAsync)
            .RequirePermission(Permission.UserManage)
            .WithName("MoveUserDepartment")
            .WithTags("Users");
    }
}
