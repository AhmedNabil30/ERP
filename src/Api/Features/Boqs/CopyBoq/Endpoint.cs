using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Boqs.CopyBoq;

/// <summary>
/// <c>POST /api/projects/{projectId}/boq/copy</c>. Fills an empty BOQ from a base or an earlier project (D-217 items 19
/// and 20). Gate on the target is <c>ProjectManage</c> plus assignment; the handler checks the same on a source project.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/boq/copy";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ProjectManage, ProjectScope.FromRoute())
            .WithName("CopyBoq")
            .WithTags("Boqs");
    }
}
