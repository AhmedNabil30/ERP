using Kaff.Domain.ChangeOrders;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;

namespace Kaff.Api.Features.Portal.RejectChangeOrder;

internal static class Handler
{
    public static Task<IResult> HandleAsync(
        Guid projectId,
        Guid id,
        Request request,
        KaffDbContext database,
        TimeProvider clock,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(currentUser);

        Guid actor = currentUser.UserId ?? Guid.Empty;

        // The client decides only at his own gate. Reject() would also end a change order waiting at another step.
        return PortalChangeOrders.DecideAsync(
            projectId,
            id,
            database,
            order => order.Status != ChangeOrderStatus.ClientApproval
                ? Result.Failure(ChangeOrderErrors.NotWaitingAtThisGate)
                : order.Reject(actor, request.Reason, clock.GetUtcNow()),
            cancellationToken);
    }
}
