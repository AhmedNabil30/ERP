using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.DayLabour.CloseEngagement;

/// <summary>
/// <c>POST /api/projects/{projectId:guid}/day-labour/engagements/{engagementId:guid}/close</c> —
/// an explicit manual close, never a timeout. KAFF-210 rule 6, decisions.md D-139 §3, D-140.
/// </summary>
/// <remarks>
/// Gated <c>DayLabourSiteManage</c> + <c>ProjectScope.FromRoute()</c>, same as every route in this
/// slice. The handler additionally checks the engagement's own project (rule 6a) — the permission
/// gate alone cannot: it only knows the route's project, not the engagement's.
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/day-labour/engagements/{engagementId:guid}/close";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.DayLabourSiteManage, ProjectScope.FromRoute())
            .WithName("CloseDayLabourEngagement")
            .WithTags("DayLabour");
    }
}
