using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Projects.ReopenProject;

/// <summary>
/// <c>POST /api/projects/{projectId}/reopen</c> reopens an archived project and restarts its follow-up period
/// (AC-417-J, D-208). Same gate as signing: <c>ProjectManage</c>, project-scoped, so role and assignment are both checked.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/reopen";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ProjectManage, ProjectScope.FromRoute())
            .WithName("ReopenProject")
            .WithTags("Projects");
    }
}
