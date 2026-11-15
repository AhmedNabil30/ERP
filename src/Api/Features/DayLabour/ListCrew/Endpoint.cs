using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.DayLabour.ListCrew;

/// <summary>
/// <c>GET /api/projects/{projectId:guid}/day-labour/crew</c> - the men engaged on this project now: name and trade, nothing
/// else. Gated <c>DailyLogWrite</c> with the project from the route, the permission the crew entry itself needs, so the
/// engineer who records hours can see whom to record them for, and role and assignment are both checked.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/day-labour/crew";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.DailyLogWrite, ProjectScope.FromRoute())
            .WithName("ListProjectCrew")
            .WithTags("DayLabour");
    }
}
