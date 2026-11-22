using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Closure.ReleaseHold;

/// <summary>
/// <c>POST /api/projects/{projectId:guid}/hold-release</c>. Finance asks for the hold release (AC-802-L). Nothing is posted:
/// the Owner approves at <c>.../hold-release/approval</c> and the posting is written then. The request carries no amount.
/// Gated <c>HoldRelease</c> with the project from the route: role and assignment are both required.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/hold-release";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.HoldRelease, ProjectScope.FromRoute())
            .WithName("ReleaseHold")
            .WithTags("Closure");
    }
}
