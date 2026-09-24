using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Treasury.ListPostings;

/// <summary>
/// <c>GET /api/treasury/postings</c> and <c>GET /api/projects/{projectId}/treasury/postings</c> — the
/// account statement of spec.md §6.1, S-033. KAFF-306. Read-only: no write, no migration.
/// </summary>
/// <remarks>
/// <para>
/// <b>Gated by <see cref="Permission.TreasuryReadCompany"/> / <see cref="Permission.TreasuryReadProject"/>,
/// and both rows are UNRESOLVED with empty grant lists today</b> — questions-for-karim.md Q90, raised
/// cutting KAFF-304, states plainly that who may read a treasury balance is unanswered and blocks the
/// HTTP surface outright. <c>PermissionEvaluator</c> refuses a permission with no matching grant to
/// everyone, Owner included (<c>RoleNotGranted</c>), so these two routes exist and are wired exactly
/// like every other Treasury endpoint, but nobody can reach them until Q90 is answered and the
/// catalogue row gets a grant. See <c>PermissionCatalogue.cs</c>'s remarks on the two rows.
/// </para>
/// <para>
/// Same two-route shape as <c>PostMovement</c> and <c>ReversePosting</c>, for the same reason: the
/// project comes from the route so authorization runs before the query string is read.
/// </para>
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string CompanyRoute = "/api/treasury/postings";
    public const string ProjectRoute = "/api/projects/{projectId:guid}/treasury/postings";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(CompanyRoute, (
                KaffDbContext database,
                CancellationToken cancellationToken,
                Guid accountId,
                DateOnly? from,
                DateOnly? to,
                PostingType? type,
                string? cursor,
                int pageSize = Handler.DefaultPageSize)
                => Handler.HandleAsync(null, accountId, from, to, type, cursor, pageSize, database, cancellationToken))
            .RequirePermission(Permission.TreasuryReadCompany)
            .WithName("ListCompanyPostings")
            .WithTags("Treasury");

        app.MapGet(ProjectRoute, (
                [FromRoute] Guid? projectId,
                KaffDbContext database,
                CancellationToken cancellationToken,
                Guid accountId,
                DateOnly? from,
                DateOnly? to,
                PostingType? type,
                string? cursor,
                int pageSize = Handler.DefaultPageSize)
                => Handler.HandleAsync(projectId, accountId, from, to, type, cursor, pageSize, database, cancellationToken))
            .RequirePermission(Permission.TreasuryReadProject, ProjectScope.FromRoute())
            .WithName("ListProjectPostings")
            .WithTags("Treasury");
    }
}
