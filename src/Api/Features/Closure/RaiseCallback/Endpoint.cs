using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Closure.RaiseCallback;

/// <summary>
/// <c>POST /api/projects/{projectId:guid}/warranty-callbacks</c>. Raises a callback against the running warranty. Gated <c>WarrantyCallbackManage</c> with the project from the route: role and assignment are both required.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/warranty-callbacks";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.WarrantyCallbackManage, ProjectScope.FromRoute())
            .WithName("RaiseWarrantyCallback")
            .WithTags("Closure");
    }
}
