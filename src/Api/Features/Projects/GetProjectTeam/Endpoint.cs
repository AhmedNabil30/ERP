using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Projects.GetProjectTeam;

/// <summary>
/// <c>GET /api/projects/{projectId}/team</c> — who is assigned to the project, by name. Gated <c>ProjectRead</c>
/// with the project from the route. Name, role and assignment level only: no contact data, nothing financial.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/team";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ProjectRead, ProjectScope.FromRoute())
            .WithName("GetProjectTeam")
            .WithTags("Projects");
    }
}
