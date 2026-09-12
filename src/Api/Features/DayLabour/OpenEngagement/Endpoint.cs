using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.DayLabour.OpenEngagement;

/// <summary>
/// <c>POST /api/projects/{projectId:guid}/day-labour/engagements</c> — opens an engagement for a
/// worker on this project. KAFF-210, decisions.md D-139 §3, D-140.
/// </summary>
/// <remarks>
/// Gated <c>DayLabourSiteManage</c> + <c>ProjectScope.FromRoute()</c> — the same permission and the
/// same route prefix as <c>DayLabour.RegisterFromSite</c> (decisions.md D-140 point 4's own table).
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/day-labour/engagements";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.DayLabourSiteManage, ProjectScope.FromRoute())
            .WithName("OpenDayLabourEngagement")
            .WithTags("DayLabour");
    }
}
