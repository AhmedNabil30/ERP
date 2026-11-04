using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.ChangeOrders.WithdrawChangeOrder;

/// <summary>
/// <c>POST /api/projects/{projectId}/change-orders/{id}/withdrawal</c>. Takes the change order back before approval. Gated <c>ChangeOrderPrepare</c> with the
/// project from the route: role and assignment are both required (spec.md section 9).
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/change-orders/{id:guid}/withdrawal";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ChangeOrderPrepare, ProjectScope.FromRoute())
            .WithName("WithdrawChangeOrder")
            .WithTags("ChangeOrders");
    }
}
