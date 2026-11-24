using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.PettyCash.ListRequests;

/// <summary>
/// <c>GET /api/projects/{projectId}/petty-cash-requests?status=</c>. The عهدة requests of one project. Gated
/// <c>DraftCreate</c> (an assigned Site Engineer, Junior or Supervisor) with the project from the route. A holder of
/// <c>DraftSubmit</c> (the supervisor) reads every request; any other holder reads only the requests they drafted.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/petty-cash-requests";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.DraftCreate, ProjectScope.FromRoute())
            .WithName("ListPettyCashRequests")
            .WithTags("PettyCash");
    }
}
