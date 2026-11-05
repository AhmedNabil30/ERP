using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Closure.RaiseSnag;

/// <summary>
/// <c>POST /api/projects/{projectId:guid}/snags</c>. Raises a snag in status open. Gated <c>SnagManage</c> with the project from the route: role and assignment are both required.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/snags";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.SnagManage, ProjectScope.FromRoute())
            .WithName("RaiseSnag")
            .WithTags("Closure");
    }
}
