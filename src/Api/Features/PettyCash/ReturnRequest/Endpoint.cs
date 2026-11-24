using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.PettyCash.ReturnRequest;

/// <summary>
/// <c>POST /api/projects/{projectId}/petty-cash-requests/{id}/return</c>. The supervisor returns a junior's draft with a
/// stored reason. Gated <c>DraftSubmit</c> with the project from the route.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/petty-cash-requests/{id:guid}/return";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.DraftSubmit, ProjectScope.FromRoute())
            .WithName("ReturnPettyCashRequest")
            .WithTags("PettyCash");
    }
}
