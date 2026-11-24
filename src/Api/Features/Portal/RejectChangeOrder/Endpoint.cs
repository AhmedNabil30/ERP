using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Api.Features.Portal.ListChangeOrders;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Portal.RejectChangeOrder;

/// <summary>
/// <c>POST /api/portal/projects/{projectId}/change-orders/{id}/reject</c>, gated <c>PortalApprove</c>. ClientApproval to Rejected with the
/// client's reason stored (R5-Q3 default, KAFF-812 AC-812-B). A rejection without a reason is refused by the entity.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/portal/projects/{projectId:guid}/change-orders/{id:guid}/reject";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.PortalApprove, ProjectScope.FromRoute())
            .Produces<PortalChangeOrder>()
            .WithName("PortalRejectChangeOrder")
            .WithTags("Portal");
    }
}
