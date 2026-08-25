using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Assignments.RevokeProjectAssignment;

/// <summary>
/// <c>POST /api/projects/{projectId}/assignments/{assignmentId}/revoke</c> — the Owner or HR takes
/// somebody off a project. KAFF-114.
/// </summary>
/// <remarks>
/// <para>
/// <b>The permission check is the <c>RequirePermission</c> line below and nowhere else.</b> Same
/// permission <c>AssignUserToProject</c> uses — <c>Permission.ProjectAssignmentManage</c>, whose
/// catalogue row is <c>ProjectScoped</c> and granted to <c>Role.Owner</c> and <c>Role.Hr</c>
/// [Verified: 2026-08-25 @ <c>PermissionCatalogue.cs</c> -&gt; the
/// <c>Permission.ProjectAssignmentManage</c> row]. The story's own permissions bullet names
/// <c>ProjectScoped</c>, which is the scope, not the permission — the permission has to be the one
/// that actually appears in the catalogue, and this is it
/// [Verified: 2026-08-25 @ <c>src/Api/Features/Assignments/AssignUserToProject/Endpoint.cs</c> -&gt;
/// <c>Map</c>]. A test fails the build if this line is absent
/// [Verified: 2026-08-25 @ <c>EndpointPermissionCoverageTests.cs</c> -&gt;
/// <c>Every_mapped_endpoint_carries_a_permission_requirement</c>] — the answer decisions.md D-068 gave
/// after D-067 shipped a sibling endpoint with an XML comment exactly like this one describing a line
/// that was not there.
/// </para>
/// <para>
/// <b>The route names both the project and the assignment.</b> The project because the permission is
/// project-scoped and <c>ProjectScope.FromRoute()</c> reads it under the default key, exactly as
/// <c>AssignUserToProject</c> does; the assignment because that is the row being closed, and the
/// handler refuses a mismatch — an id that belongs to a different project is treated as not found
/// rather than silently revoked under the wrong project's authority.
/// </para>
/// <para>
/// <b>204, not a body.</b> A revoked row has nothing to show that the assignment list (KAFF-115, not
/// this story) does not already show better, and the act's only observable effect is that the next
/// request against that project is refused — the same shape KAFF-110's deactivation and KAFF-108's
/// department move both use.
/// </para>
/// <para>
/// <b>This is not a delete route.</b> There is no <c>DELETE /api/projects/{projectId}/assignments/{id}</c>
/// anywhere in this codebase, and <c>EndpointPermissionCoverageTests.cs</c> asserts none exists
/// (<c>AC-114-F</c>) — CLAUDE.md forbids deleting a <c>ProjectAssignment</c> row, and revocation is the
/// only way this story closes one.
/// </para>
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/assignments/{assignmentId:guid}/revoke";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ProjectAssignmentManage, ProjectScope.FromRoute())
            .WithName("RevokeProjectAssignment")
            .WithTags("Assignments");
    }
}
