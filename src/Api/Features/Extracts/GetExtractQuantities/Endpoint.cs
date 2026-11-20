using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Extracts.GetExtractQuantities;

/// <summary>
/// <c>GET /api/projects/{projectId}/extracts/{id}/quantities</c>. Gated <c>ExtractRead</c> with the project from the
/// route, as the extract list is. Each BOQ line's previous, current and cumulative quantity as the extract kept them
/// (D-248); the rate and the amounts only for a viewer who may see extract money.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/extracts/{id:guid}/quantities";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ExtractRead, ProjectScope.FromRoute())
            .WithName("GetExtractQuantities")
            .WithTags("Extracts");
    }
}
