using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Closure.ReleaseSubcontractorRetention;

/// <summary>
/// <c>POST /api/projects/{projectId:guid}/subcontractors/{subcontractorId:guid}/retention-release</c>. Releases one
/// subcontractor's whole retention once the warranty has ended (KAFF-806). The request carries no amount. Gated
/// <c>SubcontractorRetentionRelease</c> with the project from the route: role and assignment are both required.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/subcontractors/{subcontractorId:guid}/retention-release";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.SubcontractorRetentionRelease, ProjectScope.FromRoute())
            .WithName("ReleaseSubcontractorRetention")
            .WithTags("Closure");
    }
}
