using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Kaff.Domain.Contracts;
using Kaff.Domain.Rulings;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Treasury.ProofOfNumbers;

/// <summary>
/// <c>GET /api/projects/{projectId}/treasury/proof</c> — the data behind إثبات الأرقام, the read-only
/// reconciliation screen of the project-journey study (2026-10-08).
/// </summary>
/// <remarks>
/// Same gate as the other Treasury reads: <c>TreasuryReadProject</c> with the route's project, so role
/// and assignment are both checked server-side (D-173). The screen itself is shown to Finance and the
/// Owner; the Site Engineer, who also holds this permission, is kept off it by the web route guard,
/// which is convenience only. Read-only: no write, no migration.
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/treasury/proof";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, (
                [FromRoute] Guid projectId,
                KaffDbContext database,
                IContractTypeDispatcher dispatcher,
                RulingSet rulings,
                CancellationToken cancellationToken)
                => Handler.HandleAsync(projectId, database, dispatcher, rulings, cancellationToken))
            .RequirePermission(Permission.TreasuryReadProject, ProjectScope.FromRoute())
            .WithName("GetProofOfNumbers")
            .WithTags("Treasury");
    }
}
