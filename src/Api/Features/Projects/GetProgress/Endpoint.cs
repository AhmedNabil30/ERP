using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Projects.GetProgress;

/// <summary>
/// <c>GET /api/projects/{projectId}/progress</c> — where the project stands, in the form its contract type allows.
/// Gated <c>ProjectRead</c> with the project from the route, so a caller with the role but no assignment is
/// refused. The percentage and the Bab completion are for every reader; the money figures only for a caller who
/// may see extract money. Derived on every read; nothing is stored.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/progress";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ProjectRead, ProjectScope.FromRoute())
            .WithName("GetProjectProgress")
            .WithTags("Projects");
    }
}
