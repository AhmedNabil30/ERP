using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Adjustments.GetAdjustment;

/// <summary>
/// <c>GET /api/projects/{projectId:guid}/adjustments/{id:guid}</c>. Reads one adjustment. Gated <c>TreasuryReadProject</c> with the project from the route: role and assignment are both required.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/adjustments/{id:guid}";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.TreasuryReadProject, ProjectScope.FromRoute())
            .WithName("GetAdjustment")
            .WithTags("Adjustments");
    }
}
