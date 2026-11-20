using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Extracts.PreviewDeductions;

/// <summary>
/// <c>GET /api/projects/{projectId}/extracts/{id}/deductions-preview</c>. The figures applying the deductions would produce,
/// shown to Finance before it applies them (D-260 item 3). Gated <c>ExtractDeductionsApply</c>, as the step is; role and
/// assignment. Changes and posts nothing.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/extracts/{id:guid}/deductions-preview";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ExtractDeductionsApply, ProjectScope.FromRoute())
            .WithName("PreviewDeductions")
            .WithTags("Extracts");
    }
}
