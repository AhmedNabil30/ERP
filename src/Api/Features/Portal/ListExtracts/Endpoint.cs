using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Portal.ListExtracts;

/// <summary><c>GET /api/portal/projects/{projectId}/extracts</c>, gated <c>PortalRead</c> on the project in the route.</summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/portal/projects/{projectId:guid}/extracts";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.PortalRead, ProjectScope.FromRoute())
            .Produces<IReadOnlyList<PortalExtract>>()
            .WithName("PortalListExtracts")
            .WithTags("Portal");
    }
}
