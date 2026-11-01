using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.DailyLogs.OpenDailyLog;

/// <summary>
/// <c>/api/projects/{projectId}/daily-logs</c>. Opens, or reaches again, the caller's log for the day the given time falls on. KAFF-600 rule 1. Gated <c>DailyLogWrite</c> with the project from
/// the route, so a Site Engineer who is not assigned to the project is refused (spec.md section 9).
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/daily-logs";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.DailyLogWrite, ProjectScope.FromRoute())
            .WithName("OpenDailyLog")
            .WithTags("DailyLogs");
    }
}
