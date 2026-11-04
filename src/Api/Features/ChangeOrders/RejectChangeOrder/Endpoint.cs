using System.Security.Claims;
using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Api.Common.Validation;
using Kaff.Domain.Authorization;
using Kaff.Domain.ChangeOrders;
using Kaff.Domain.Identity;
using Kaff.Domain.Rulings;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.ChangeOrders.RejectChangeOrder;

/// <summary>
/// Three routes, one handler. A rejection ends the change order at Rejected with a stored reason (spec.md section
/// 13), and the permission to reject is the permission to decide at that gate, so each gate has its own route. A
/// route called for a change order not waiting at its gate is refused.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string TechnicalReviewRoute = "/api/projects/{projectId:guid}/change-orders/{id:guid}/technical-rejection";
    public const string ClientApprovalRoute = "/api/projects/{projectId:guid}/change-orders/{id:guid}/client-rejection";
    public const string OwnerRoute = "/api/projects/{projectId:guid}/change-orders/{id:guid}/owner-rejection";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        MapGate(app, TechnicalReviewRoute, Permission.ChangeOrderReview, "RejectChangeOrderAtTechnicalReview", ChangeOrderStatus.TechnicalReview);
        MapGate(app, ClientApprovalRoute, Permission.ChangeOrderClientApprovalRecord, "RejectChangeOrderAtClientApproval", ChangeOrderStatus.ClientApproval);
        MapGate(app, OwnerRoute, Permission.ChangeOrderApprove, "RejectChangeOrderAtOwner", ChangeOrderStatus.OwnerApproval);
    }

    private static void MapGate(
        IEndpointRouteBuilder app, string route, Permission permission, string name, ChangeOrderStatus waitingAt)
        => app.MapPost(route, (
                [FromRoute] Guid projectId,
                [FromRoute] Guid id,
                Request request,
                KaffDbContext database,
                RulingSet rulings,
                TimeProvider clock,
                ICurrentUser currentUser,
                IAuthorizationService authorization,
                ClaimsPrincipal user,
                CancellationToken cancellationToken)
                => Handler.HandleAsync(
                    projectId, id, request, waitingAt, database, rulings, clock, currentUser, authorization, user, cancellationToken))
            .RequirePermission(permission, ProjectScope.FromRoute())
            .AddEndpointFilter<ValidationFilter<Request>>()
            .WithName(name)
            .WithTags("ChangeOrders");
}
