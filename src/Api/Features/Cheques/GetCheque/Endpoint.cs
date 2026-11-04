using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Cheques.GetCheque;

/// <summary>
/// <c>GET /api/projects/{projectId}/cheques/{id}</c>. One cheque: its state and the collection it belongs to.
/// Gated <c>ExtractCollect</c> (the row that moves a cheque, holder unresolved, Q97) with the project from the
/// route, so a user without the role or without the assignment is refused.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/cheques/{id:guid}";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ExtractCollect, ProjectScope.FromRoute())
            .WithName("GetCheque")
            .WithTags("Cheques");
    }
}
