using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Api.Common.Validation;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Extracts.CollectExtract;

/// <summary>
/// <c>Post /api/projects/{projectId}/extracts/{id}/collect</c>. Finance records that the client paid an issued extract. Gated ExtractCollect, holder unresolved (Q97). Role and assignment are both required (spec.md section 9).
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/extracts/{id:guid}/collect";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ExtractCollect, ProjectScope.FromRoute())
            .AddEndpointFilter<ValidationFilter<Request>>()
            .WithName("CollectExtract")
            .WithTags("Extracts");
    }
}
