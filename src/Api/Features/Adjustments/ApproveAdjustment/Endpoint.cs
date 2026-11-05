using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Adjustments.ApproveAdjustment;

/// <summary>
/// <c>POST /api/projects/{projectId:guid}/adjustments/{id:guid}/approval</c>. The Owner approves and the credit note posts in the same save. Gated <c>FinancialMovementApprove</c> with the project from the route: role and assignment are both required.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/adjustments/{id:guid}/approval";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.FinancialMovementApprove, ProjectScope.FromRoute())
            .WithName("ApproveAdjustment")
            .WithTags("Adjustments");
    }
}
