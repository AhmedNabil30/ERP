using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Opportunities.AdvanceOpportunityStage;

/// <summary><c>POST /api/opportunities/{opportunityId:guid}/advance</c>. Needs <c>OpportunityManage</c> (Owner, Marketing).</summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/opportunities/{opportunityId:guid}/advance";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.OpportunityManage)
            .WithName("AdvanceOpportunityStage")
            .WithTags("Opportunities");
    }
}
