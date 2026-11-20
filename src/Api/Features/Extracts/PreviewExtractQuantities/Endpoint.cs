using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Extracts.PreviewExtractQuantities;

/// <summary>
/// <c>GET /api/projects/{projectId}/extracts/quantities-preview</c>. Gated <c>ExtractPrepare</c>, project-scoped, as
/// prepare is: the quantities the next extract would copy if prepared now, in the shape of the extract's own
/// quantities read. Refused with prepare's own refusal when prepare would be refused.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/extracts/quantities-preview";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ExtractPrepare, ProjectScope.FromRoute())
            .WithName("PreviewExtractQuantities")
            .WithTags("Extracts");
    }
}
