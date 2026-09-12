using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.DayLabour.ListEngagements;

/// <summary>
/// <c>GET /api/projects/{projectId:guid}/day-labour/engagements?workerId={guid}</c> — one worker's
/// engagement history on this project, with the day rate and the pool's three figures for him.
/// KAFF-210, decisions.md D-152 §2 (Q76), D-153 §1.
/// </summary>
/// <remarks>
/// Gated <c>DayLabourRateManage</c> + <c>ProjectScope.FromRoute()</c>, the same row
/// <c>SetEngagementDayRate</c> uses — the read and the write share a gate; the handler narrows who
/// sees which engagements, not the catalogue.
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/day-labour/engagements";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.DayLabourRateManage, ProjectScope.FromRoute())
            .WithName("ListWorkerEngagements")
            .WithTags("DayLabour");
    }
}
