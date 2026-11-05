using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Closure.DeclarePracticalCompletion;

/// <summary>
/// <c>POST /api/projects/{projectId:guid}/practical-completion</c>. Declares practical completion: Active to HandoverPending. Gated <c>ProjectPracticalCompletionDeclare</c> with the project from the route: role and assignment are both required.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/practical-completion";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ProjectPracticalCompletionDeclare, ProjectScope.FromRoute())
            .WithName("DeclarePracticalCompletion")
            .WithTags("Closure");
    }
}
