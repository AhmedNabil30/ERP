using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Ncrs.CloseNcr;

/// <summary>
/// <c>POST /api/projects/{projectId:guid}/ncrs/{id:guid}/close</c>. Closes the NCR with a note and releases the held
/// portion of its extract (spec.md section 7, AC-500-E). Gated <see cref="NcrSupport.ClosePermission"/> with the
/// project from the route: role and assignment are both required.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/ncrs/{id:guid}/close";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(NcrSupport.ClosePermission, ProjectScope.FromRoute())
            .WithName("CloseNcr")
            .WithTags("Extracts");
    }
}
