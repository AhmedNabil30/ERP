using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;

namespace Kaff.Api.Features.Opportunities.AdvanceOpportunityStage;

internal static class Handler
{
    public static Task<IResult> HandleAsync(
        Guid opportunityId,
        KaffDbContext database,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(clock);

        return OpportunitySupport.TransitionAsync(
            opportunityId, database, opportunity => opportunity.AdvanceStage(clock.GetUtcNow()), cancellationToken);
    }
}
