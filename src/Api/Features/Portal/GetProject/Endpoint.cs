using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Portal.GetProject;

/// <summary>
/// <c>GET /api/portal/projects/{projectId}</c>, gated <c>PortalRead</c> with the project in the route. The gate asks the access
/// policy whether this project's client is the caller's client, so another client's project is refused before the handler runs,
/// with the same blanket refusal a project that does not exist gets (spec.md section 12: never another client's data).
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/portal/projects/{projectId:guid}";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.PortalRead, ProjectScope.FromRoute())
            .Produces<PortalProjectHome>()
            .WithName("PortalGetProject")
            .WithTags("Portal");
    }
}
