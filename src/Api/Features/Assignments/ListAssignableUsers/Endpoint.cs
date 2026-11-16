using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Assignments.ListAssignableUsers;

/// <summary>
/// <c>GET /api/projects/{projectId}/assignments/candidates</c> — who the add sheet may offer: active people not yet on the
/// project whom <c>AssignUserToProject</c> would accept. Same permission and scope as that route (D-227 item 2): whoever may
/// add may see the candidates, and nobody else may.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/assignments/candidates";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ProjectAssignmentManage, ProjectScope.FromRoute())
            .WithName("ListAssignableUsers")
            .WithTags("Assignments");
    }
}
