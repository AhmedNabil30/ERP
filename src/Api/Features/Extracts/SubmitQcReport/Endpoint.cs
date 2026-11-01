using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Extracts.SubmitQcReport;

/// <summary>
/// <c>Post /api/projects/{projectId}/extracts/{id}/qc-report</c>. The weekly QC report is in (spec.md section 7). Gated ExtractQcReportSubmit, holder unresolved (D-189 section 3). Role and assignment are both required (spec.md section 9).
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/extracts/{id:guid}/qc-report";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ExtractQcReportSubmit, ProjectScope.FromRoute())
            .WithName("SubmitQcReport")
            .WithTags("Extracts");
    }
}
