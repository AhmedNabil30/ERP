using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Extracts.ApproveExtractAsOwner;

/// <summary>
/// <c>Post /api/projects/{projectId}/extracts/{id}/owner-approval</c>. The Owner approves every extract, no threshold (spec.md section 7). Reuses FinancialMovementApprove (D-189 section 4). Role and assignment are both required (spec.md section 9).
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/extracts/{id:guid}/owner-approval";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.FinancialMovementApprove, ProjectScope.FromRoute())
            .WithName("ApproveExtractAsOwner")
            .WithTags("Extracts");
    }
}
