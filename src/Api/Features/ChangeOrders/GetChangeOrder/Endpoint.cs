using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.ChangeOrders.GetChangeOrder;

/// <summary>
/// <c>GET /api/projects/{projectId}/change-orders/{id}</c>. Gated <c>ProjectRead</c> with the project from the route,
/// so an unassigned caller is refused. The price is included only for a holder of a change-order permission; the
/// <c>actions</c> block says which controls this caller may use.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/change-orders/{id:guid}";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ProjectRead, ProjectScope.FromRoute())
            .WithName("GetChangeOrder")
            .WithTags("ChangeOrders");
    }
}
