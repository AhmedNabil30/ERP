using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.DailyLogs.ListDayLogs;

/// <summary>
/// <c>GET /api/projects/{projectId}/daily-logs?date=</c>. Every engineer log of one day on the project, with the back-dated flag
/// (D-217 item 8). Gated <c>DailyLogReview</c> with the project from the route: the owner, and the supervising site engineer
/// assigned to it. It is how they reach a log to correct.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/daily-logs";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.DailyLogReview, ProjectScope.FromRoute())
            .WithName("ListDayLogs")
            .WithTags("DailyLogs");
    }
}
