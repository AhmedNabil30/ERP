using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Users.DeactivateUser;

/// <summary>
/// <c>POST /api/users/{userId}/deactivate</c> — the Owner switches somebody's account off. KAFF-110.
/// </summary>
/// <remarks>
/// <para>
/// <b>The permission check is the <c>RequirePermission</c> line below and nowhere else.</b> It names
/// <c>Permission.UserManage</c>, whose catalogue row is <c>CompanyWide</c> and granted to
/// <c>Role.Owner</c> alone [Verified: 2026-08-24 @ <c>PermissionCatalogue.cs</c> -&gt; the
/// <c>Permission.UserManage</c> row] — D-044 ruling 1. That is <c>AC-110-I</c>: HR and Finance are
/// refused here, and HR is refused despite holding <c>ProjectAssignmentManage</c>, because ending
/// somebody's access is not staffing a project. A test fails the build if the line is absent
/// [Verified: 2026-08-24 @ <c>EndpointPermissionCoverageTests.cs</c> -&gt;
/// <c>Every_mapped_endpoint_carries_a_permission_requirement</c>], decisions.md D-067 and D-069.
/// </para>
/// <para>
/// Company-wide, so no scope is declared: the route carries a <i>user</i> id and
/// <c>ProjectScope.FromRoute()</c> would find no project and refuse every caller including the Owner.
/// </para>
/// <para>
/// <b>Nothing here ends the target's sessions, and that is the story.</b> No claim is added, no cache
/// is invalidated, no token is revoked and no revocation list exists — <c>AC-110-A</c> and
/// <c>AC-110-B</c> are already satisfied by the mechanism decisions.md D-048 built: the token carries
/// only the user id, and role, department, client scope and <b>whether the account is still active</b>
/// are re-read from the database on every authorized request
/// [Verified: 2026-08-24 @ <c>PermissionAuthorizationHandler.cs</c> -&gt; <c>BuildSubjectAsync</c>;
/// @ <c>PermissionSubjectReader.cs</c> -&gt; <c>ReadAsync</c>, whose <c>WHERE</c> filters
/// <c>user.IsActive</c> before any role is considered]. <b>Adding a revocation mechanism here would
/// be a second answer to a question already answered, and the two would disagree.</b>
/// </para>
/// <para>
/// <b><c>AC-110-B</c> is exercised separately from <c>AC-110-A</c> on purpose.</b> The subject read
/// used to be reached only when a request named a project, so every company-wide permission was
/// decided from token claims alone and a deactivated Owner kept <c>UserManage</c> — finding F-11,
/// closed by D-048. A test that only exercised the project-scoped path would have passed throughout.
/// </para>
/// <para>
/// <b>POST, not DELETE.</b> A user is never deleted (KAFF-110 rule 6, D-049 ruling 5): the audit
/// trail names actors by id, so removing the row makes every record they wrote unreadable.
/// Deactivation is an act performed on an account that continues to exist, and it carries an optional
/// body.
/// </para>
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/users/{userId:guid}/deactivate";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.UserManage)
            .WithName("DeactivateUser")
            .WithTags("Users");
    }
}
