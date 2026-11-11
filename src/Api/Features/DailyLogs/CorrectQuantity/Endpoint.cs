using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.DailyLogs.CorrectQuantity;

/// <summary>
/// <c>/api/projects/{projectId}/daily-logs/quantities/{entryId}/corrections</c>. Corrects a saved quantity (D-218 item 14).
/// Gated <c>DailyLogWrite</c> with the project from the route, so a site engineer who is not assigned is refused and the owner
/// is refused (whether the owner may correct a quantity is not ruled). The handler then allows today to the engineer who entered
/// it and an earlier open day to the supervising site engineer.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/daily-logs/quantities/{entryId:guid}/corrections";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.DailyLogWrite, ProjectScope.FromRoute())
            .WithName("CorrectQuantity")
            .WithTags("DailyLogs");
    }
}
