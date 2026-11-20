using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Ncrs.ListNcrs;

/// <summary>
/// <c>GET /api/projects/{projectId:guid}/ncrs</c>. The project's NCRs, open first. Gated <c>ExtractRead</c> with the
/// project from the route, like the extract list: role and assignment are both required.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/ncrs";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ExtractRead, ProjectScope.FromRoute())
            .WithName("ListNcrs")
            .WithTags("Extracts");
    }
}
