using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Extracts.GetExtract;

/// <summary>
/// <c>GET /api/projects/{projectId}/extracts/{id}</c>. Gated <c>ProjectRead</c> with the project from the route, so
/// an unassigned caller is refused. <b>What the body carries depends on the caller</b>: the figures and the
/// quantity evidence are included only for a holder of the document, deduction or approval permission, and the
/// <c>actions</c> block says which controls this caller may use.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/extracts/{id:guid}";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ProjectRead, ProjectScope.FromRoute())
            .WithName("GetExtract")
            .WithTags("Extracts");
    }
}
