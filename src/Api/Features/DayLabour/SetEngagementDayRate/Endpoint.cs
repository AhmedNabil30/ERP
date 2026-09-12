using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.DayLabour.SetEngagementDayRate;

/// <summary>
/// <c>PUT /api/projects/{projectId:guid}/day-labour/engagements/{engagementId:guid}/day-rate</c> —
/// records the agreed day rate. KAFF-210, decisions.md D-152 §2 (Q76), D-153 §1.
/// </summary>
/// <remarks>
/// Gated <c>DayLabourRateManage</c> + <c>ProjectScope.FromRoute()</c> — the new row D-153 §1 splits off
/// <c>DayLabourSiteManage</c>, so this route reaches the Owner, Finance and the responsible Site
/// Engineer, and the handler's own guard narrows the write to the Owner or the opener.
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/day-labour/engagements/{engagementId:guid}/day-rate";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPut(Route, Handler.HandleAsync)
            .RequirePermission(Permission.DayLabourRateManage, ProjectScope.FromRoute())
            .WithName("SetEngagementDayRate")
            .WithTags("DayLabour");
    }
}
