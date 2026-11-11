using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Extracts.GetWaitingOn;

/// <summary>
/// <c>GET /api/projects/{projectId}/waiting-on</c> — what the project's open extracts are waiting for, and on whom.
/// Gated <c>ProjectRead</c> with the project from the route. Role and assignment are enforced by the gate; the body
/// holds no money.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/waiting-on";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ExtractRead, ProjectScope.FromRoute())
            .WithName("GetWaitingOn")
            .WithTags("Extracts");
    }
}
