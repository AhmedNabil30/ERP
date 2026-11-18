using System.Security.Claims;
using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Kaff.Domain.Identity;
using Kaff.Domain.Rulings;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Cheques.MoveCheque;

/// <summary>
/// Three routes: a client's cheque is deposited, then cleared or bounced (spec.md section 6.5). The cheque's own state
/// machine decides whether the move is legal. D-235 item 1: the clearance is the move that makes the cheque cash, so it
/// posts the collection and may complete the extract; a deposit and a bounce post nothing, and a bounce needs a reason.
/// Gated ExtractCollect, holder unresolved (Q97). Role and assignment are both required (spec.md section 9).
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string DepositRoute = "/api/projects/{projectId:guid}/cheques/{id:guid}/deposit";
    public const string ClearRoute = "/api/projects/{projectId:guid}/cheques/{id:guid}/clearance";
    public const string BounceRoute = "/api/projects/{projectId:guid}/cheques/{id:guid}/bounce";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(DepositRoute, Handler.DepositAsync)
            .RequirePermission(Permission.ExtractCollect, ProjectScope.FromRoute())
            .WithName("DepositCheque")
            .WithTags("Cheques");

        app.MapPost(ClearRoute, (
                [FromRoute] Guid projectId,
                [FromRoute] Guid id,
                KaffDbContext database,
                RulingSet rulings,
                TimeProvider clock,
                ICurrentUser currentUser,
                IAuthorizationService authorization,
                ClaimsPrincipal user,
                CancellationToken cancellationToken)
                => Handler.SettleAsync(
                    projectId, id, null, false, new(database, rulings, clock, currentUser, authorization, user), cancellationToken))
            .RequirePermission(Permission.ExtractCollect, ProjectScope.FromRoute())
            .WithName("ClearCheque")
            .WithTags("Cheques");

        app.MapPost(BounceRoute, (
                [FromRoute] Guid projectId,
                [FromRoute] Guid id,
                [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] BounceRequest? request,
                KaffDbContext database,
                RulingSet rulings,
                TimeProvider clock,
                ICurrentUser currentUser,
                IAuthorizationService authorization,
                ClaimsPrincipal user,
                CancellationToken cancellationToken)
                => Handler.SettleAsync(
                    projectId, id, request?.Reason, true, new(database, rulings, clock, currentUser, authorization, user), cancellationToken))
            .RequirePermission(Permission.ExtractCollect, ProjectScope.FromRoute())
            .WithName("BounceCheque")
            .WithTags("Cheques");
    }
}

/// <summary>Why the cheque bounced. Required and stored on the extract's gate record (D-235 item 1).</summary>
/// <param name="Reason">The bank's reason, as Finance records it.</param>
public sealed record BounceRequest(string? Reason);
