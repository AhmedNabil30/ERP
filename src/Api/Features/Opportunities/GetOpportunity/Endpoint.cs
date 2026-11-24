using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Opportunities.GetOpportunity;

/// <summary><c>GET /api/opportunities/{opportunityId:guid}</c>. One opportunity in the list's row shape. Needs <c>OpportunityManage</c> (Owner, Marketing).</summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/opportunities/{opportunityId:guid}";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.OpportunityManage)
            .WithName("GetOpportunity")
            .WithTags("Opportunities");
    }
}
