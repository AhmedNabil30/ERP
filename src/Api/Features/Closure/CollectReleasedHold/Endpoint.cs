using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Api.Common.Validation;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using CollectRequest = Kaff.Api.Features.Extracts.CollectExtract.Request;

namespace Kaff.Api.Features.Closure.CollectReleasedHold;

/// <summary>
/// D-244 item 2. <c>POST /api/projects/{projectId}/hold-release/collect</c>: Finance records that the client paid the
/// released hold, in full or in part, by cash, transfer or cheque, with the extract collection's body.
/// <c>GET /api/projects/{projectId}/hold-release/collection</c>: where the released hold stands. A cheque moves through the
/// cheque routes. Both gated <c>ExtractCollect</c> (Finance) with the project from the route: role and assignment are
/// both required.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string CollectRoute = "/api/projects/{projectId:guid}/hold-release/collect";
    public const string ReadRoute = "/api/projects/{projectId:guid}/hold-release/collection";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(CollectRoute, Handler.CollectAsync)
            .RequirePermission(Permission.ExtractCollect, ProjectScope.FromRoute())
            .AddEndpointFilter<ValidationFilter<CollectRequest>>()
            .WithName("CollectReleasedHold")
            .WithTags("Closure");

        app.MapGet(ReadRoute, Handler.ReadAsync)
            .RequirePermission(Permission.ExtractCollect, ProjectScope.FromRoute())
            .WithName("GetReleasedHoldCollection")
            .WithTags("Closure");
    }
}
