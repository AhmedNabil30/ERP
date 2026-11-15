using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.DailyLogs.GetPhoto;

/// <summary>
/// <c>GET /api/projects/{projectId}/daily-logs/photos/{reference}</c>. Streams a stored site photo. Gated <c>DailyLogRead</c>
/// with the project from the route (the owner and any assigned site engineer); the handler narrows a junior engineer to the
/// photos he recorded, the same rule as the log reads.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/daily-logs/photos/{reference:guid}";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.DailyLogRead, ProjectScope.FromRoute())
            .WithName("GetPhoto")
            .WithTags("DailyLogs");
    }
}
