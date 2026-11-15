using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.DayLabour.ListCrew;

/// <summary>The open engagements of the route's project, joined to the man and his باب. A read: no audit record.</summary>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(Guid projectId, KaffDbContext database, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        // Projected in the query: the rate, phone and code columns are never read.
        List<CrewMember> items = await (
            from engagement in database.Engagements.AsNoTracking()
            join worker in database.Employees.AsNoTracking() on engagement.WorkerId equals worker.Id
            join bab in database.Babs.AsNoTracking() on worker.BabId equals bab.Id into trades
            from trade in trades.DefaultIfEmpty()
            where engagement.ProjectId == projectId && engagement.ClosedAt == null
            orderby worker.FullName
            select new CrewMember(worker.Id, worker.FullName, trade == null ? null : trade.NameAr))
            .ToListAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(new Response(items));
    }
}
