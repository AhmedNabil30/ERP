using Kaff.Domain.ChangeOrders;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;

namespace Kaff.Api.Features.Portal.ApproveChangeOrder;

internal static class Handler
{
    public static Task<IResult> HandleAsync(
        Guid projectId,
        Guid id,
        KaffDbContext database,
        TimeProvider clock,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(currentUser);

        Guid actor = currentUser.UserId ?? Guid.Empty;

        // RecordClientApproval refuses anything not at ClientApproval and anyone who acted on it before (separation of duties).
        return PortalChangeOrders.DecideAsync(
            projectId, id, database, order => order.RecordClientApproval(actor, clock.GetUtcNow()), cancellationToken);
    }
}
