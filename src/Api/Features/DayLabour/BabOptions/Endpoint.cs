using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Api.Features.Employees.ListBabOptions;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.DayLabour.BabOptions;

/// <summary>
/// <c>GET /api/projects/{projectId:guid}/day-labour/babs</c> — the trade picker for a Site Engineer.
/// decisions.md D-140 point 7.
/// </summary>
/// <remarks>
/// <b>Maps straight onto <c>Employees.ListBabOptions.Handler.HandleAsync</c> — no copy of the query.</b>
/// D-140 point 7 rejects the opposite case, one route returning two shapes: this is two routes sharing
/// one shape instead. Only the gate differs, <c>DayLabourSiteManage</c> + <c>ProjectScope.FromRoute()</c>
/// rather than <c>EmployeeManage</c>.
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/day-labour/babs";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.DayLabourSiteManage, ProjectScope.FromRoute())
            .WithName("DayLabourBabOptions")
            .WithTags("DayLabour");
    }
}
