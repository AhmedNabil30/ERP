using System.Security.Claims;
using Kaff.Domain.ChangeOrders;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Domain.Rulings;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Kaff.Api.Features.ChangeOrders.RejectChangeOrder;

/// <summary>Rejects at the gate the route names. The change order ends at Rejected with the reason stored (ChangeOrderRejection).</summary>
internal static class Handler
{
    public static Task<IResult> HandleAsync(
        Guid projectId,
        Guid id,
        Request request,
        ChangeOrderStatus waitingAt,
        KaffDbContext database,
        RulingSet rulings,
        TimeProvider clock,
        ICurrentUser currentUser,
        IAuthorizationService authorization,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return ChangeOrderSupport.RunAsync(
            projectId,
            id,
            new ChangeOrderServices(database, rulings, clock, currentUser, authorization, user),
            (order, project, services) => order.Status != waitingAt
                ? Result.Failure(ChangeOrderErrors.NotWaitingAtThisGate)
                : order.Reject(ChangeOrderSupport.Actor(services), request.Reason, ChangeOrderSupport.Now(services)),
            cancellationToken);
    }
}
