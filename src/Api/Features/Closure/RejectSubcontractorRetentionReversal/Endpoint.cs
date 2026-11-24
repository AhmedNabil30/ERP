using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Closure.RejectSubcontractorRetentionReversal;

/// <summary>
/// <c>POST /api/projects/{projectId:guid}/subcontractors/{subcontractorId:guid}/retention-release/reversal/rejection</c>. The Owner returns the waiting reversal request to Finance with a stored reason. Nothing is posted. Gated
/// <c>FinancialMovementApprove</c> with the project from the route: role and assignment are both required.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/subcontractors/{subcontractorId:guid}/retention-release/reversal/rejection";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.FinancialMovementApprove, ProjectScope.FromRoute())
            .WithName("RejectSubcontractorRetentionReversal")
            .WithTags("Closure");
    }
}
