using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Adjustments.RaiseAdjustment;

/// <summary>
/// <c>POST /api/projects/{projectId:guid}/adjustments</c>. Raises an adjustment and posts nothing; the Owner's approval
/// posts. Gated <c>AdjustmentRaise</c> with the project from the route: role and assignment are both required.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/adjustments";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.AdjustmentRaise, ProjectScope.FromRoute())
            .WithName("RaiseAdjustment")
            .WithTags("Adjustments");
    }
}
