using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Projects.GetProject;

/// <summary>
/// <c>GET /api/projects/{projectId}</c> — one project's header, for S-052. KAFF-400.
/// </summary>
/// <remarks>
/// <para>
/// <b>The permission check is the <c>RequirePermission</c> line below and nowhere else.</b> It names
/// <c>Permission.ProjectRead</c> with <c>ProjectScope.FromRoute()</c> — <c>ProjectScoped</c> in the
/// catalogue, granted to Owner, Finance, Technical Office, Marketing, Site Engineer and Head of Design
/// (spec.md §9). <b>A caller with the role but no assignment to this project is refused</b> — the
/// story's own AC-400-B: role alone does not admit anyone past <c>IProjectAccessPolicy</c>, Owner and
/// HR's global reach aside, and HR does not hold this row at all (D-044 ruling 2).
/// </para>
/// <para>
/// <b>HR is refused here, not redacted.</b> HR's own project view — name, code, team size, nothing
/// financial — already exists at <c>GET /api/auth/me</c> (<c>TeamProjectEntry</c>, KAFF-105b, D-103).
/// Granting HR this row instead would duplicate that mechanism and put every financial field on this
/// response in front of HR the moment a route change forgot to strip one; the catalogue keeps the two
/// surfaces structurally separate instead.
/// </para>
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ProjectRead, ProjectScope.FromRoute())
            .WithName("GetProject")
            .WithTags("Projects");
    }
}
