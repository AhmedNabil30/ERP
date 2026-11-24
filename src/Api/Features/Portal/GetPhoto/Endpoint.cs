using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Portal.GetPhoto;

/// <summary>
/// <c>GET /api/portal/projects/{projectId}/photos/{photoId}</c>, gated <c>PortalRead</c>. The bytes of one published photo. It returns a
/// file, not a JSON body, so it declares no response type for the reflection gate to walk: nothing about the project travels with it.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/portal/projects/{projectId:guid}/photos/{photoId:guid}";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.PortalRead, ProjectScope.FromRoute())
            .WithName("PortalGetPhoto")
            .WithTags("Portal");
    }
}
