using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Boqs.GetBoq;

/// <summary><c>GET /api/projects/{projectId}/boq</c>. Same gate as the writes: the BOQ shows cost prices.</summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/boq";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ProjectManage, ProjectScope.FromRoute())
            .WithName("GetBoq")
            .WithTags("Boqs");
    }
}
