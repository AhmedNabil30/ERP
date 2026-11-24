using Kaff.Domain.Auditing;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;

namespace Kaff.Api.Features.Opportunities.ReopenOpportunity;

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

        if (!string.IsNullOrWhiteSpace(request?.Reason))
        {
            audit.SetReason(request.Reason.Trim());
        }

        return OpportunitySupport.TransitionAsync(
            opportunityId, database, opportunity => opportunity.Reopen(clock.GetUtcNow()), cancellationToken);
    }
}
