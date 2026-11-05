using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Closure.TerminateProject;

/// <summary>
/// <c>POST /api/projects/{projectId:guid}/termination</c>. Always refused: Terminated is unruled (Q108), so no settlement is invented. Gated <c>ProjectManage</c> with the project from the route: role and assignment are both required.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/termination";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ProjectManage, ProjectScope.FromRoute())
            .WithName("TerminateProject")
            .WithTags("Closure");
    }
}
