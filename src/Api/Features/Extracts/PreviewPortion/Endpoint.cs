using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Extracts.PreviewPortion;

/// <summary>
/// <c>GET /api/projects/{projectId}/extracts/{id}/portion-preview?portion=0.7</c>. What a Technical Office approval of that
/// portion would approve for payment and hold (D-260 item 2), so the screen shows both figures as the percentage is typed
/// and the browser does no money arithmetic (D-135). Gated <c>QuantityGateApprove</c>, as the gate is; role and assignment.
/// Changes nothing.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/extracts/{id:guid}/portion-preview";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.QuantityGateApprove, ProjectScope.FromRoute())
            .WithName("PreviewPortion")
            .WithTags("Extracts");
    }
}
