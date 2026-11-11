using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.DailyLogs.CorrectDayLabour;

/// <summary>
/// <c>/api/projects/{projectId}/daily-logs/day-labour/{entryId}/corrections</c>. Corrects saved crew hours (D-217 item 3,
/// D-218 item 16). Gated <c>DailyLogCrewCorrect</c> with the project from the route: the owner, and a site engineer assigned to
/// the project. The handler then allows today to the engineer who entered it, an earlier open day to the supervising site
/// engineer, and any open day to the owner.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/daily-logs/day-labour/{entryId:guid}/corrections";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.DailyLogCrewCorrect, ProjectScope.FromRoute())
            .WithName("CorrectDayLabour")
            .WithTags("DailyLogs");
    }
}
