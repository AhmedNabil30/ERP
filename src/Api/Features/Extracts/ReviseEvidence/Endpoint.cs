using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Extracts.ReviseEvidence;

/// <summary>
/// <c>POST /api/projects/{projectId}/extracts/{id}/evidence-revision</c>. Gated <c>ExtractPrepare</c>, the
/// preparer's own permission (no revise permission was ruled; see <c>ExtractRevisionScope</c>), project-scoped:
/// role and assignment are both required (spec.md section 9).
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/extracts/{id:guid}/evidence-revision";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ExtractPrepare, ProjectScope.FromRoute())
            .WithName("ReviseExtractEvidence")
            .WithTags("Extracts");
    }
}
