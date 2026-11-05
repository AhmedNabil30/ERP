using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Closure.CloseProject;

/// <summary>
/// <c>POST /api/projects/{projectId:guid}/close</c>. Closes a project in UnderWarranty only when every account reads settled from the ledgers. Gated <c>ProjectClose</c> with the project from the route: role and assignment are both required.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/close";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ProjectClose, ProjectScope.FromRoute())
            .WithName("CloseProject")
            .WithTags("Closure");
    }
}
