using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Closure.BeginWarranty;

/// <summary>
/// <c>POST /api/projects/{projectId:guid}/warranty</c>. Moves a handed-over project to UnderWarranty (Q110, an explicit act). Gated <c>ProjectHandover</c> with the project from the route: role and assignment are both required.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/warranty";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ProjectHandover, ProjectScope.FromRoute())
            .WithName("BeginWarranty")
            .WithTags("Closure");
    }
}
