using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.DayLabour.RegisterFromSite;

/// <summary>
/// <c>POST /api/projects/{projectId:guid}/day-labour</c> — a Site Engineer registers one day
/// labourer from site. KAFF-209, decisions.md D-140.
/// </summary>
/// <remarks>
/// <para>
/// <b>The permission check is the <c>RequirePermission</c> line below and nowhere else.</b>
/// <c>Permission.DayLabourSiteManage</c> is <c>ProjectScoped</c>, granted to <c>Role.Owner</c> and any
/// assigned Site Engineer (Junior or Supervisor)
/// [Verified: 2026-09-12 @ <c>PermissionCatalogue.cs</c> -&gt; the
/// <c>Permission.DayLabourSiteManage</c> row]. HR is deliberately absent — D-140 point 1 — so this
/// route never becomes a second way into the salaried register.
/// </para>
/// <para>
/// <b><c>ProjectScope.FromRoute()</c>, not the body.</b> D-140 point 2: the project authorises the
/// act; the pool itself stays company-wide (point 3), so no project id is ever read out of
/// <c>Request</c>.
/// </para>
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/day-labour";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.DayLabourSiteManage, ProjectScope.FromRoute())
            .WithName("RegisterDayLabourerFromSite")
            .WithTags("DayLabour");
    }
}
