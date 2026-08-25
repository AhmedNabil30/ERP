using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Assignments.AssignUserToProject;

/// <summary>
/// <c>POST /api/projects/{projectId}/assignments</c> — the Owner or HR puts somebody on a project.
/// KAFF-113.
/// </summary>
/// <remarks>
/// <para>
/// <b>The permission check is the <c>RequirePermission</c> line below and nowhere else.</b> It names
/// <c>Permission.ProjectAssignmentManage</c>, whose catalogue row is <c>ProjectScoped</c> and granted
/// to <c>Role.Owner</c> and <c>Role.Hr</c>
/// [Verified: 2026-08-24 @ <c>PermissionCatalogue.cs</c> -&gt; the
/// <c>Permission.ProjectAssignmentManage</c> row]. A test fails the build if this line is absent
/// [Verified: 2026-08-24 @ <c>EndpointPermissionCoverageTests.cs</c> -&gt;
/// <c>Every_mapped_endpoint_carries_a_permission_requirement</c>], which is the answer decisions.md
/// D-068 gave to a comment exactly like this one that described a line that was not there (D-067).
/// </para>
/// <para>
/// <b>The scope is <c>ProjectScope.FromRoute()</c> and the route names a project, even though HR
/// reaches every project without an assignment row.</b> Those are two different rules and only one
/// of them lives here. The permission stays project-scoped, so the assignment half of spec.md §9's
/// "role × assignment" is evaluated on every call and the request must name a project that exists;
/// HR's <i>reach</i> is the access policy's answer, and it is bounded by the same project existing
/// [Verified: 2026-08-24 @ <c>ProjectAccessPolicy.cs</c> -&gt; <c>GlobalReachAsync</c>]. Widening the
/// catalogue row to <c>CompanyWide</c> to "solve" HR's reach would delete the requirement that a real
/// project be named — which is <c>AC-113-C</c>, and it would also be refused by
/// <c>Every_permission_requirement_declares_the_scope_its_catalogue_row_names</c>.
/// </para>
/// <para>
/// <b>So HR staffs a project it was never assigned to, and still cannot open it</b>
/// (<c>AC-113-A</c>, <c>AC-113-B</c>). Reach is not capability: HR is deliberately absent from
/// <c>Permission.ProjectRead</c> (D-044 ruling 2), so the same global reach that admits HR here
/// admits it to nothing financial. Requiring an assignment in order to create assignments is
/// circular — on a new project nobody is assigned, so nobody could make the first row (D-044
/// ruling 3).
/// </para>
/// <para>
/// <b>Being on the project is not permission to staff it</b> (<c>AC-113-G</c>). A Supervisor site
/// engineer assigned to this very project holds no <c>ProjectAssignmentManage</c> grant, so the
/// role half refuses them before the assignment half is consulted at all.
/// </para>
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/assignments";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ProjectAssignmentManage, ProjectScope.FromRoute())
            .WithName("AssignUserToProject")
            .WithTags("Assignments");
    }
}
