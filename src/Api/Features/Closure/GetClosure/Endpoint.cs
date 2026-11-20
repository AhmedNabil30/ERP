using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Closure.GetClosure;

/// <summary>
/// <c>GET /api/projects/{projectId:guid}/closure</c>. What the closure page shows above its actions: the hold to be
/// released and every extract not yet Collected or Void (D-257). Gated <c>ProjectRead</c> with the project from the
/// route: role and assignment are both required. The hold is null for a viewer who does not see money.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/closure";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ProjectRead, ProjectScope.FromRoute())
            .WithName("GetClosure")
            .WithTags("Closure");
    }
}
