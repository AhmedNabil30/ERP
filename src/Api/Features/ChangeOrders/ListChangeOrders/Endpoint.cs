using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.ChangeOrders.ListChangeOrders;

/// <summary>
/// <c>GET /api/projects/{projectId}/change-orders</c>. Gated <c>ProjectRead</c> with the project from the route. The
/// rows are money-free; the price is on the single read for those who may see it.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/change-orders";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ProjectRead, ProjectScope.FromRoute())
            .WithName("ListChangeOrders")
            .WithTags("ChangeOrders");
    }
}
