using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Adjustments.ListAdjustments;

/// <summary>
/// <c>GET /api/projects/{projectId:guid}/adjustments</c>. Lists a project's adjustments. Gated <c>TreasuryReadProject</c> with the project from the route: role and assignment are both required.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/adjustments";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.TreasuryReadProject, ProjectScope.FromRoute())
            .WithName("ListAdjustments")
            .WithTags("Adjustments");
    }
}
