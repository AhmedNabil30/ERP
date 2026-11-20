using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Closure.HandOverProject;

/// <summary>
/// <c>POST /api/projects/{projectId:guid}/handover</c>. Hands the project over: HandoverPending to Handover, stamping the handover date and the warranty end. Refused while a major snag is unverified, while an NCR on the project is open, or while a released remainder is not yet billed on an extract (D-254 item 2). Gated <c>ProjectHandover</c> with the project from the route: role and assignment are both required.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/handover";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ProjectHandover, ProjectScope.FromRoute())
            .WithName("HandOverProject")
            .WithTags("Closure");
    }
}
