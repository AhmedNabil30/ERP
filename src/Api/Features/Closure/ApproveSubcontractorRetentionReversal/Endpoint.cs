using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Closure.ApproveSubcontractorRetentionReversal;

/// <summary>
/// <c>POST /api/projects/{projectId:guid}/subcontractors/{subcontractorId:guid}/retention-release/reversal/approval</c>. The Owner approves the waiting reversal request and the one mirror posting, pointing at the release through ReversesId, is written in the same save. The user who asked may not approve. Gated
/// <c>FinancialMovementApprove</c> with the project from the route: role and assignment are both required.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/subcontractors/{subcontractorId:guid}/retention-release/reversal/approval";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.FinancialMovementApprove, ProjectScope.FromRoute())
            .WithName("ApproveSubcontractorRetentionReversal")
            .WithTags("Closure");
    }
}
