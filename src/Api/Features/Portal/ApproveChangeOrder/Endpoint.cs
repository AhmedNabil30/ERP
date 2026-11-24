using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Api.Features.Portal.ListChangeOrders;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Portal.ApproveChangeOrder;

/// <summary>
/// <c>POST /api/portal/projects/{projectId}/change-orders/{id}/approve</c>, gated <c>PortalApprove</c> on the project in the route.
/// ClientApproval to OwnerApproval with the Client user as actor (R5-Q3 default, KAFF-812 AC-812-A). The contract value does not move
/// until the Owner approves.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/portal/projects/{projectId:guid}/change-orders/{id:guid}/approve";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.PortalApprove, ProjectScope.FromRoute())
            .Produces<PortalChangeOrder>()
            .WithName("PortalApproveChangeOrder")
            .WithTags("Portal");
    }
}
