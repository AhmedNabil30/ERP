using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Api.Common.Validation;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Users.CreateUser;

/// <summary>
/// <c>POST /api/users</c> — the Owner creates a user. KAFF-106.
/// </summary>
/// <remarks>
/// <para>
/// <b>The permission check is the <c>RequirePermission</c> line below and nowhere else.</b> It names
/// <c>Permission.UserManage</c>, whose catalogue row is <c>CompanyWide</c> and granted to
/// <c>Role.Owner</c> alone [Verified: 2026-08-23 @ <c>PermissionCatalogue.cs</c> ->
/// the <c>Permission.UserManage</c> row] — Karim, 2026-08-20, decisions.md D-044 ruling 1: "strictly
/// Global and held exclusively by the Owner". Both halves of "role × assignment" are decided by
/// <c>PermissionEvaluator</c> from that row: the role half against the grants, and the assignment
/// half by the scope, which is company-wide and therefore names no project to be assigned to.
/// Declaring <c>ProjectScope.FromRoute()</c> on a route with no project in it would refuse every
/// caller including the Owner.
/// </para>
/// <para>
/// The role is read from the database on every request, not from the token
/// [Verified: 2026-08-23 @ <c>PermissionAuthorizationHandler.cs</c> -> <c>BuildSubjectAsync</c>], so
/// a deactivated Owner and a forged <c>role=Owner</c> claim are both refused here — decisions.md
/// D-048.
/// </para>
/// <para>
/// <b>HR is refused by this line too.</b> HR holds <c>ProjectAssignmentManage</c> and staffs projects
/// with users that already exist; it does not mint logins, because whoever sets a user's department
/// can grant project-assignment power (<c>AC-106-C</c>, D-044 ruling 1).
/// </para>
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/users";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.UserManage)
            .AddEndpointFilter<ValidationFilter<Request>>()
            .WithName("CreateUser")
            .WithTags("Users");
    }
}
