using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.ChangeOrders.RecordClientApproval;

/// <summary>
/// <c>POST /api/projects/{projectId}/change-orders/{id}/client-approval</c>. ClientApproval to OwnerApproval. Records that the client approved; who records it before the portal exists is Q103. Gated <c>ChangeOrderClientApprovalRecord</c> with the
/// project from the route: role and assignment are both required (spec.md section 9).
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/change-orders/{id:guid}/client-approval";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ChangeOrderClientApprovalRecord, ProjectScope.FromRoute())
            .WithName("RecordClientApproval")
            .WithTags("ChangeOrders");
    }
}
