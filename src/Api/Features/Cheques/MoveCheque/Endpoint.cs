using System.Security.Claims;
using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Kaff.Domain.Collections;
using Kaff.Domain.Identity;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Cheques.MoveCheque;

/// <summary>
/// Three routes, one handler: a client's cheque is deposited, then cleared or bounced (spec.md section 6.5). Each
/// route names the state it moves to; the cheque's own state machine decides whether the move is legal. Gated
/// ExtractCollect, holder unresolved (Q97). Role and assignment are both required (spec.md section 9). A move
/// writes audit and posts nothing: the cheque accounts are unruled (Q91).
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string DepositRoute = "/api/projects/{projectId:guid}/cheques/{id:guid}/deposit";
    public const string ClearRoute = "/api/projects/{projectId:guid}/cheques/{id:guid}/clearance";
    public const string BounceRoute = "/api/projects/{projectId:guid}/cheques/{id:guid}/bounce";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        MapMove(app, DepositRoute, ChequeState.Deposited, "DepositCheque");
        MapMove(app, ClearRoute, ChequeState.Cleared, "ClearCheque");
        MapMove(app, BounceRoute, ChequeState.Bounced, "BounceCheque");
    }

    private static void MapMove(IEndpointRouteBuilder app, string route, ChequeState target, string name)
        => app.MapPost(route, (
                [FromRoute] Guid projectId,
                [FromRoute] Guid id,
                KaffDbContext database,
                TimeProvider clock,
                ICurrentUser currentUser,
                ClaimsPrincipal user,
                CancellationToken cancellationToken)
                => Handler.HandleAsync(projectId, id, target, database, clock, currentUser, cancellationToken))
            .RequirePermission(Permission.ExtractCollect, ProjectScope.FromRoute())
            .WithName(name)
            .WithTags("Cheques");
}
