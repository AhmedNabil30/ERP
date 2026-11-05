using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Closure.VerifySnag;

/// <summary>
/// <c>POST /api/projects/{projectId:guid}/snags/{id:guid}/verification</c>. Fixed to verified. Gated <c>SnagManage</c> with the project from the route: role and assignment are both required.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/snags/{id:guid}/verification";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.SnagManage, ProjectScope.FromRoute())
            .WithName("VerifySnag")
            .WithTags("Closure");
    }
}
