using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Boqs.RemoveBoqLine;

/// <summary><c>DELETE /api/projects/{projectId}/boq/lines/{lineId}</c>. Draft BOQ only; a frozen one refuses.</summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/boq/lines/{lineId:guid}";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapDelete(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ProjectManage, ProjectScope.FromRoute())
            .WithName("RemoveBoqLine")
            .WithTags("Boqs");
    }
}
