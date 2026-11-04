using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.ChangeOrders.ApproveChangeOrderAsOwner;

/// <summary>
/// <c>POST /api/projects/{projectId}/change-orders/{id}/owner-approval</c>. OwnerApproval to Approved. Only the Owner; Finance never approves a change order (spec.md section 9). The contract value then includes it. Gated <c>ChangeOrderApprove</c> with the
/// project from the route: role and assignment are both required (spec.md section 9).
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/change-orders/{id:guid}/owner-approval";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ChangeOrderApprove, ProjectScope.FromRoute())
            .WithName("ApproveChangeOrderAsOwner")
            .WithTags("ChangeOrders");
    }
}
