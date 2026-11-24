using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Closure.GetSubcontractorRetention;

/// <summary>
/// <c>GET /api/projects/{projectId}/subcontractor-retention</c>. What the closure screen shows for subcontractor retention.
/// Gated <c>SubcontractorRetentionRead</c> (Finance, Owner, Technical Office, D-273 answer 4) with the project from the route:
/// role and assignment are both required.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/subcontractor-retention";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.SubcontractorRetentionRead, ProjectScope.FromRoute())
            .WithName("GetSubcontractorRetention")
            .WithTags("Closure");
    }
}
