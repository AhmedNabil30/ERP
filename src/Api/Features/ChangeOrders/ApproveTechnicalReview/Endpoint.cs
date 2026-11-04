using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.ChangeOrders.ApproveTechnicalReview;

/// <summary>
/// <c>POST /api/projects/{projectId}/change-orders/{id}/technical-review</c>. TechnicalReview to ClientApproval. A different person from everyone who acted so far. Gated <c>ChangeOrderReview</c> with the
/// project from the route: role and assignment are both required (spec.md section 9).
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/change-orders/{id:guid}/technical-review";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ChangeOrderReview, ProjectScope.FromRoute())
            .WithName("ApproveTechnicalReview")
            .WithTags("ChangeOrders");
    }
}
