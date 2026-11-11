using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Boqs.AddBoqLines;

/// <summary>
/// <c>POST /api/projects/{projectId}/boq/lines/batch</c>. Several catalogue items in one request, all or nothing
/// (D-217, "what this needs"). Same gate as the single add.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/boq/lines/batch";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ProjectManage, ProjectScope.FromRoute())
            .WithName("AddBoqLines")
            .WithTags("Boqs");
    }
}
