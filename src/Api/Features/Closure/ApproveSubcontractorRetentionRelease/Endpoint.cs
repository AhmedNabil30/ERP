using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Closure.ApproveSubcontractorRetentionRelease;

/// <summary>
/// <c>POST /api/projects/{projectId:guid}/subcontractors/{subcontractorId:guid}/retention-release/approval</c>. The Owner
/// approves the waiting retention release request and the one <c>SubcontractorRetentionRelease</c> posting is written in the
/// same save, for the retention balance summed from postings at that moment. Gated <c>FinancialMovementApprove</c> with the
/// project from the route (spec.md section 9). The user who asked may not approve.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/subcontractors/{subcontractorId:guid}/retention-release/approval";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.FinancialMovementApprove, ProjectScope.FromRoute())
            .WithName("ApproveSubcontractorRetentionRelease")
            .WithTags("Closure");
    }
}
