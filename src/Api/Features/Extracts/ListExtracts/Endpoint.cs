using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Extracts.ListExtracts;

/// <summary>
/// <c>GET /api/projects/{projectId}/extracts</c>. Gated <c>ProjectRead</c> with the project from the route.
/// A row is money-free: number, period, status. The figures are read per extract.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/extracts";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ProjectRead, ProjectScope.FromRoute())
            .WithName("ListExtracts")
            .WithTags("Extracts");
    }
}
