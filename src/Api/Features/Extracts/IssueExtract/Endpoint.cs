using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Extracts.IssueExtract;

/// <summary>
/// <c>Post /api/projects/{projectId}/extracts/{id}/issue</c>. Issues the approved extract to the client. ASSUMED gate: no permission row exists for issuing, so this reuses the Owner's FinancialMovementApprove. See D-194. Role and assignment are both required (spec.md section 9).
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/extracts/{id:guid}/issue";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.FinancialMovementApprove, ProjectScope.FromRoute())
            .WithName("IssueExtract")
            .WithTags("Extracts");
    }
}
