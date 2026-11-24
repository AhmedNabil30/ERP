using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.DailyLogs.PublishPhoto;

/// <summary>
/// <c>POST /api/projects/{projectId}/daily-logs/photos/{reference}/publish</c> - R5-C, spec.md section 8: a photo reaches the client only
/// when somebody decides it should. Gated <c>SitePhotoPublish</c> (the supervising engineer and the Technical Office, R5-Q12 assumed) on the
/// project in the route, so a junior engineer is refused and so is anyone not assigned to the project.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/daily-logs/photos/{reference:guid}/publish";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.SitePhotoPublish, ProjectScope.FromRoute())
            .WithName("PublishPhoto")
            .WithTags("DailyLogs");
    }
}
