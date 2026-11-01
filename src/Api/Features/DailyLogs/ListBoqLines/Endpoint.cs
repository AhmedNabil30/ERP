using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.DailyLogs.ListBoqLines;

/// <summary>
/// <c>GET /api/projects/{projectId}/daily-logs/boq-lines</c>. Lists the lines a quantity can be recorded against,
/// money-free. Gated <c>DailyLogWrite</c> with the project from the route, the same gate as
/// <c>RecordQuantity</c> (D-191): listing the lines you may record against is the same act as recording.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/daily-logs/boq-lines";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.DailyLogWrite, ProjectScope.FromRoute())
            .WithName("ListBoqLinesForDailyLog")
            .WithTags("DailyLogs");
    }
}
