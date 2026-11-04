using Kaff.Domain.ChangeOrders;
using Kaff.Domain.Common;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.ChangeOrders;

/// <summary>Reads shared by the change-order slice and by the features that show or bill their value.</summary>
internal static class ChangeOrderQueries
{
    /// <summary>
    /// The sum of the approved change orders, per project. Derived from the lines on every call; there is no stored
    /// total. A project with none is absent from the result. <paramref name="projectIds"/> null means every project.
    /// </summary>
    public static async Task<Dictionary<Guid, Money>> ApprovedTotalsAsync(
        KaffDbContext database, IReadOnlyCollection<Guid>? projectIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        IQueryable<ChangeOrder> query = database.ChangeOrders
            .AsNoTracking()
            .Include(order => order.Lines)
            .Where(order => order.Status == ChangeOrderStatus.Approved);
        if (projectIds is not null)
        {
            query = query.Where(order => projectIds.Contains(order.ProjectId));
        }

        List<ChangeOrder> approved = await query.ToListAsync(cancellationToken);
        return approved
            .GroupBy(order => order.ProjectId)
            .ToDictionary(group => group.Key, group => Money.Sum(group.Select(order => order.Total)));
    }

    public static async Task<Money> ApprovedTotalAsync(KaffDbContext database, Guid projectId, CancellationToken cancellationToken)
    {
        Dictionary<Guid, Money> totals = await ApprovedTotalsAsync(database, [projectId], cancellationToken);
        return totals.TryGetValue(projectId, out Money total) ? total : Money.Zero;
    }
}
