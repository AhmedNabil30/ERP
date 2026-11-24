using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Extracts.GetMaterialAdvance;

/// <summary>
/// <c>GET /api/projects/{projectId}/material-advance</c>. The تشوينات issued, recovered and still outstanding on a
/// project, for the supervising engineer at the QC step and for the roles that already read project money (KAFF-309).
/// Gated <c>ProjectMoneyRead</c> with the project from the route: the junior never receives it.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/material-advance";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, ([FromRoute] Guid projectId, KaffDbContext database, CancellationToken cancellationToken)
                => Handler.HandleAsync(projectId, database, cancellationToken))
            .RequirePermission(Permission.ProjectMoneyRead, ProjectScope.FromRoute())
            .WithName("GetMaterialAdvance")
            .WithTags("Extracts");
    }
}
