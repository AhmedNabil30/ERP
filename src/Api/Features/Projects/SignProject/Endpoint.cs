using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Projects.SignProject;

/// <summary>
/// <c>POST /api/projects/{projectId}/sign</c> — sign the contract: take the BOQ total as the contract
/// value, freeze the BOQ and stamp the date, in one transaction.
/// </summary>
/// <remarks>
/// <b>Permission: <c>ProjectManage</c>, a stand-in.</b> The project journey study (decision 7) found that
/// contract signature has no permission row of its own. Owner and Technical Office hold
/// <c>ProjectManage</c>, project-scoped, which is the nearest honest fit; a dedicated row is Nabil's to
/// rule. Role and assignment are both checked here, server-side.
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/sign";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ProjectManage, ProjectScope.FromRoute())
            .WithName("SignProject")
            .WithTags("Projects");
    }
}
