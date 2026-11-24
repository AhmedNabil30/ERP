using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Portal.ListProjects;

/// <summary>
/// <c>GET /api/portal/projects</c> - the client's own projects (R5-B AC-B-1, spec.md section 12). No project in the route, so no
/// project-scoped permission can name one: the route is self-only and the handler scopes the query to the caller's client file,
/// which is read from the database and never from the request.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/portal/projects";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePortalSession()
            .Produces<IReadOnlyList<PortalProjectSummary>>()
            .WithName("PortalListProjects")
            .WithTags("Portal");
    }
}
