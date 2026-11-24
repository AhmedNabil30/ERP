using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.PettyCash.SubmitRequest;

/// <summary>
/// <c>POST /api/projects/{projectId}/petty-cash-requests/{id}/submission</c>. The supervisor submits a junior's draft
/// (spec.md section 9). Gated <c>DraftSubmit</c> with the project from the route: role, level and assignment are all required.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/petty-cash-requests/{id:guid}/submission";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.DraftSubmit, ProjectScope.FromRoute())
            .WithName("SubmitPettyCashRequest")
            .WithTags("PettyCash");
    }
}
