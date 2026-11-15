using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.DailyLogs.UploadPhoto;

/// <summary>
/// <c>POST /api/projects/{projectId}/daily-logs/photos/upload</c>. Takes one image as the raw request body and keeps it on the
/// server (D-225 item 2). Gated <c>DailyLogWrite</c> with the project from the route: role and assignment, as for recording the
/// photo. It writes no log entry; <c>RecordPhoto</c> does, naming the reference this returns.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/daily-logs/photos/upload";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.DailyLogWrite, ProjectScope.FromRoute())
            .WithName("UploadPhoto")
            .WithTags("DailyLogs");
    }
}
