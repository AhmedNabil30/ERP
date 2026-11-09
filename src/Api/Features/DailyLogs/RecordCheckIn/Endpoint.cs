using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.DailyLogs.RecordCheckIn;

/// <summary>
/// <c>POST /api/projects/{projectId}/daily-logs/check-ins</c>. Records a check-in or check-out with the phone's
/// location. Gated <c>DailyLogWrite</c> with the project from the route, the same as every daily log entry, so an
/// engineer who is not assigned to the project is refused (spec.md section 9). KAFF-613.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/daily-logs/check-ins";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.DailyLogWrite, ProjectScope.FromRoute())
            .WithName("RecordCheckIn")
            .WithTags("DailyLogs");
    }
}
