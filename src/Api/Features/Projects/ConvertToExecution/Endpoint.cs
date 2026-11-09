using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Projects.ConvertToExecution;

/// <summary>
/// <c>POST /api/projects/{projectId}/convert-to-execution</c> — a design project opens its execution project. KAFF-415.
/// </summary>
/// <remarks>
/// <b>The permission check is the two <c>RequirePermission</c> lines below and nowhere else.</b> Opening a project is
/// <c>ProjectCreate</c>, company-wide, as on <c>CreateProject</c>; acting on the design project is <c>ProjectManage</c>
/// with the project from the route, so a caller with the role but no assignment to the design project is refused. Both
/// rows are held by the Owner and the Technical Office. ASP.NET Core requires every policy on an endpoint to pass.
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/convert-to-execution";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ProjectCreate)
            .RequirePermission(Permission.ProjectManage, ProjectScope.FromRoute())
            .WithName("ConvertToExecution")
            .WithTags("Projects");
    }
}
