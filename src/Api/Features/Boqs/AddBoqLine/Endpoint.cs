using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Boqs.AddBoqLine;

/// <summary>
/// <c>POST /api/projects/{projectId}/boq/lines</c>. <c>ProjectManage</c> is project-scoped and held by the
/// Owner and the Technical Office, who own the BOQ (spec.md §2); a holder not assigned to the project is
/// refused. The BOQ carries cost prices, so it is not opened to <c>ProjectRead</c> holders.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/boq/lines";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ProjectManage, ProjectScope.FromRoute())
            .WithName("AddBoqLine")
            .WithTags("Boqs");
    }
}
