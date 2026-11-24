using Kaff.Domain.Auditing;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;

namespace Kaff.Api.Features.Opportunities.CloseOpportunityLost;

internal static class Handler
{
    public static Task<IResult> HandleAsync(
        Guid opportunityId,
        KaffDbContext database,
        IAuditContext audit,
        TimeProvider clock,
        CancellationToken cancellationToken,
        Request? request = null)
    {
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(clock);

        string reason = request?.Reason ?? string.Empty;

        // The audit record of the close carries the reason (R3-04 AC-C), and the row keeps it too.
        if (!string.IsNullOrWhiteSpace(reason))
        {
            audit.SetReason(reason.Trim());
        }

        return OpportunitySupport.TransitionAsync(
            opportunityId, database, opportunity => opportunity.CloseLost(reason, clock.GetUtcNow()), cancellationToken);
    }
}
