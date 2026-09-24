using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Projects.ListProjects;

/// <summary>
/// <c>GET /api/projects</c> — every project the caller may reach. KAFF-400, S-050.
/// </summary>
/// <remarks>
/// <para>
/// <b>The permission check is the <c>RequirePermission</c> line below and nowhere else.</b> It names
/// <c>Permission.ProjectList</c>, a new <c>CompanyWide</c> row (KAFF-400) granted to <c>Role.Owner</c>,
/// <c>Role.Finance</c>, <c>Role.TechnicalOffice</c>, <c>Role.MarketingSales</c> and
/// <c>Role.SiteEngineer</c>. Company-wide, and deliberately so: a list request cannot name the one
/// project it is about, exactly the reasoning that split <c>ProjectCreate</c> off
/// <c>ProjectManage</c> (decisions.md D-055 §3). <c>Permission.ProjectRead</c> — the row
/// <c>GetProject</c> uses — stays <c>ProjectScoped</c> and is <b>not</b> reused here for that reason:
/// declaring it with no scope would refuse every caller, Owner included, with
/// <c>ProjectNotSpecified</c>.
/// </para>
/// <para>
/// <b>HR and the portal Client are refused by this one line.</b> HR holds no grant on
/// <c>ProjectList</c> — its own project list is <c>GET /api/auth/me</c>'s <c>TeamProjectEntry</c>
/// (KAFF-105b, D-103), deliberately separate so a financial field can never reach HR through a route
/// that forgot to strip one (D-044 ruling 2, "zero financial visibility"). <c>Role.Client</c> holds
/// only <c>PortalRead</c>/<c>PortalApprove</c> (spec.md §12) and is refused the same way.
/// </para>
/// <para>
/// <b>Reach through this row is "may call the endpoint", not "may see every row".</b> The permission
/// gate admits the request; <c>Handler</c> still filters what comes back to spec.md §9's assignment
/// rule — see its own remarks.
/// </para>
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ProjectList)
            .WithName("ListProjects")
            .WithTags("Projects");
    }
}
