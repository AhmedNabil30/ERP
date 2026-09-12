using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.DayLabour.ListEngagements;

/// <summary>
/// One worker's engagement history on the route's project, with the day rate and the three pool
/// figures for him. KAFF-210, decisions.md D-152 §2 (Q76), D-153 §1.
/// </summary>
/// <remarks>
/// <para>
/// <b>Filtered to the route's project</b> — D-153 §1 point 6's own read rule: the Owner and Finance
/// see every engagement on the route's project; a caller whose role is Site Engineer sees only
/// engagements they opened. The route's permission (<c>DayLabourRateManage</c>) is itself
/// project-scoped, so a Finance or Site Engineer caller could otherwise be asked for a rate on a
/// project neither is assigned to — filtering here keeps the read inside what the grant actually
/// authorised.
/// </para>
/// <para>
/// <b>The three figures are derived from exactly the set this read returns</b>, never a stored column
/// (KAFF-210 rule 2) — a Site Engineer restricted to their own engagements sees the average of those,
/// not of engagements they hold no rate visibility over.
/// </para>
/// <para>No audit record. It is a read.</para>
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        Guid workerId,
        KaffDbContext database,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(currentUser);

        bool workerExists = await database.Employees
            .AnyAsync(employee => employee.Id == workerId && employee.Kind == EmployeeKind.DayLabour, cancellationToken);

        if (!workerExists)
        {
            return ResultExtensions.Problem(MasterDataErrors.EmployeeNotFound);
        }

        Role? callerRole = await EngagementResponsibility.CallerRoleAsync(database, currentUser.UserId, cancellationToken);

        IQueryable<Engagement> query = database.Engagements
            .Where(engagement => engagement.WorkerId == workerId && engagement.ProjectId == projectId);

        if (callerRole == Role.SiteEngineer)
        {
            query = query.Where(engagement => engagement.OpenedByUserId == currentUser.UserId);
        }

        List<Engagement> engagements = await query
            .OrderBy(engagement => engagement.OpenedAt)
            .ToListAsync(cancellationToken);

        List<EngagementEntry> items =
        [
            .. engagements.Select(engagement => new EngagementEntry(
                engagement.Id,
                engagement.ProjectId,
                engagement.OpenedOn,
                engagement.ClosedOn,
                engagement.DayRate,
                engagement.Rating)),
        ];

        List<Money> rates = [.. engagements.Where(e => e.DayRate is not null).Select(e => e.DayRate!.Value)];
        List<int> ratings = [.. engagements.Where(e => e.Rating is not null).Select(e => e.Rating!.Value)];

        Money? averageDayRate = rates.Count == 0 ? null : Money.From(rates.Sum(rate => rate.Amount) / rates.Count);
        decimal? averageRating = ratings.Count == 0 ? null : (decimal)ratings.Sum() / ratings.Count;

        return Microsoft.AspNetCore.Http.Results.Ok(
            new Response(workerId, items, averageDayRate, engagements.Count, averageRating));
    }
}
