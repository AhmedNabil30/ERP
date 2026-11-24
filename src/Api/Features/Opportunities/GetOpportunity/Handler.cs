using Kaff.Api.Common.Results;
using Kaff.Api.Features.Opportunities.ListOpportunities;
using Kaff.Domain.Rulings;
using Kaff.Domain.Sales;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Opportunities.GetOpportunity;

internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid opportunityId,
        KaffDbContext database,
        RulingSet rulings,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(rulings);
        ArgumentNullException.ThrowIfNull(clock);

        var row = await database.Opportunities.AsNoTracking()
            .Where(o => o.Id == opportunityId)
            .Join(database.Clients, o => o.ClientId, c => c.Id, (o, c) => new { Opportunity = o, ClientName = c.Name, ClientPhone = c.PhoneEntered })
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return ResultExtensions.Problem(SalesErrors.OpportunityNotFound);
        }

        OpportunityInactivity rule = rulings.OpportunityInactivity;
        DateTimeOffset now = clock.GetUtcNow();
        return Microsoft.AspNetCore.Http.Results.Ok(new OpportunitySummary(
            row.Opportunity.Id,
            row.Opportunity.Code,
            row.Opportunity.ClientId,
            row.ClientName,
            row.ClientPhone,
            row.Opportunity.Title,
            row.Opportunity.Stage,
            row.Opportunity.StatusAt(now, rule),
            row.Opportunity.InactivityAt(now, rule),
            row.Opportunity.ClosedLostReason,
            row.Opportunity.LastActivityAt));
    }
}
