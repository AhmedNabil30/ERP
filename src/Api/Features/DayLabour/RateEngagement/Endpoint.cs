using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.DayLabour.RateEngagement;

/// <summary>
/// <c>POST /api/projects/{projectId:guid}/day-labour/engagements/{engagementId:guid}/rate</c> —
/// records a rating out of 5. KAFF-210 rule 7, decisions.md D-139 §3.
/// </summary>
/// <remarks>
/// Gated <c>DayLabourSiteManage</c> + <c>ProjectScope.FromRoute()</c>, same as every route in this
/// slice, plus the handler's own rule 6a project-match check.
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/day-labour/engagements/{engagementId:guid}/rate";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.DayLabourSiteManage, ProjectScope.FromRoute())
            .WithName("RateDayLabourEngagement")
            .WithTags("DayLabour");
    }
}
