using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Extracts.GetCollection;

/// <summary>
/// <c>GET /api/projects/{projectId}/extracts/{id}/collection</c>. The collections recorded against one extract,
/// oldest first, each with the id of its cheque when it was paid by cheque. That id is how a client reaches the
/// cheque routes. Gated <c>ExtractCollect</c> (the same row that records a collection, holder unresolved, Q97)
/// with the project from the route: the body carries money figures, so it is not offered to every assigned user.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/extracts/{id:guid}/collection";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ExtractCollect, ProjectScope.FromRoute())
            .WithName("GetExtractCollection")
            .WithTags("Extracts");
    }
}
