using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Treasury.GetBalances;

/// <summary>
/// <c>GET /api/projects/{projectId}/treasury/balances</c> — the five ledgers of spec.md §6.4, S-040.
/// <c>GET /api/treasury/accounts/{accountId}/balance</c> — one account's derived balance, S-036.
/// KAFF-306. Read-only: no write, no migration.
/// </summary>
/// <remarks>
/// Both figures already exist at the Domain layer (<c>BalanceQueries</c>, KAFF-304); this slice is
/// only the HTTP surface. <b>Gated by <see cref="Permission.TreasuryReadProject"/> /
/// <see cref="Permission.TreasuryReadCompany"/>, both UNRESOLVED with empty grants</b> —
/// questions-for-karim.md Q90 blocks who may read a treasury balance over HTTP. See
/// <c>ListPostings.Endpoint</c>'s remarks and <c>PermissionCatalogue.cs</c> for the same note; it is
/// not repeated in full here.
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string ProjectLedgersRoute = "/api/projects/{projectId:guid}/treasury/balances";
    public const string AccountBalanceRoute = "/api/treasury/accounts/{accountId:guid}/balance";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(ProjectLedgersRoute, (
                [FromRoute] Guid projectId,
                KaffDbContext database)
                => Handler.HandleLedgers(projectId, database))
            .RequirePermission(Permission.TreasuryReadProject, ProjectScope.FromRoute())
            .WithName("GetProjectLedgerBalances")
            .WithTags("Treasury");

        app.MapGet(AccountBalanceRoute, (
                [FromRoute] Guid accountId,
                KaffDbContext database)
                => Handler.HandleAccount(accountId, database))
            .RequirePermission(Permission.TreasuryReadCompany)
            .WithName("GetAccountBalance")
            .WithTags("Treasury");
    }
}
