using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Opportunities.ListOpportunities;

/// <summary><c>GET /api/opportunities</c>. Needs <c>OpportunityManage</c> (Owner, Marketing). R3-01, R3-03.</summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/opportunities";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.OpportunityManage)
            .WithName("ListOpportunities")
            .WithTags("Opportunities");
    }
}
