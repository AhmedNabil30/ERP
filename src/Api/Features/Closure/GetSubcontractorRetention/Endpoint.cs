using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Closure.GetSubcontractorRetention;

/// <summary>
/// <c>GET /api/projects/{projectId}/subcontractor-retention</c>. What the closure screen shows for subcontractor retention.
/// Gated <c>ProjectRead</c> with the project from the route (role and assignment); the handler then refuses a caller who
/// is not Technical Office (D-273 answer 4) and holds neither <c>SubcontractorRetentionRelease</c> (Finance asks) nor <c>FinancialMovementApprove</c> (the Owner decides).
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/subcontractor-retention";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ProjectRead, ProjectScope.FromRoute())
            .WithName("GetSubcontractorRetention")
            .WithTags("Closure");
    }
}
