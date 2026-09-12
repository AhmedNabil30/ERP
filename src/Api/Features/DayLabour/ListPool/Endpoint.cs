using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.DayLabour.ListPool;

/// <summary>
/// <c>GET /api/projects/{projectId:guid}/day-labour</c> — the pool, for picking a worker.
/// decisions.md D-140 point 4.
/// </summary>
/// <remarks>
/// Gated <c>DayLabourSiteManage</c> + <c>ProjectScope.FromRoute()</c>. The project makes the read
/// lawful; the pool returned is company-wide (D-140 point 3), not filtered to this project.
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/day-labour";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.DayLabourSiteManage, ProjectScope.FromRoute())
            .WithName("ListDayLabourPool")
            .WithTags("DayLabour");
    }
}
