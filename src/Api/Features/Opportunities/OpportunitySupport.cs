using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.Sales;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Opportunities;

/// <summary>
/// The shared shape of an opportunity transition: find the row, let the entity decide, save. The audit
/// record (who, when, before and after) is written by the save interceptor, not here.
/// </summary>
internal static class OpportunitySupport
{
    public static async Task<IResult> TransitionAsync(
        Guid opportunityId,
        KaffDbContext database,
        Func<Opportunity, Result> transition,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(transition);

        Opportunity? opportunity = await database.Opportunities
            .FirstOrDefaultAsync(candidate => candidate.Id == opportunityId, cancellationToken);

        if (opportunity is null)
        {
            return ResultExtensions.Problem(SalesErrors.OpportunityNotFound);
        }

        Result result = transition(opportunity);

        if (result.IsFailure)
        {
            return ResultExtensions.Problem(result.Error);
        }

        await database.SaveChangesAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.NoContent();
    }
}
