using Kaff.Api.Features.Extracts;
using Kaff.Domain.ChangeOrders;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.ChangeOrders.ListChangeOrders;

/// <summary>One row of the list. Money-free on purpose.</summary>
/// <param name="WaitingOn">Who it waits on, and for what. Null once it has ended.</param>
public sealed record ChangeOrderSummary(
    Guid Id, int Number, string Title, ChangeOrderStatus Status, bool IsPriced, WaitingLineResponse? WaitingOn);

internal static class Handler
{
    public static async Task<IResult> HandleAsync(Guid projectId, KaffDbContext database, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        List<ChangeOrder> orders = await database.ChangeOrders
            .AsNoTracking()
            .Include(order => order.GateHistory)
            .Where(order => order.ProjectId == projectId)
            .OrderBy(order => order.Number)
            .ToListAsync(cancellationToken);
        IReadOnlyList<StaffMember> staff = await ExtractWaitingOnView.StaffAsync(database, projectId, cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(orders
            .Select(order => new ChangeOrderSummary(
                order.Id, order.Number, order.Title, order.Status, order.IsPriced, ChangeOrderView.WaitingOn(order, staff)))
            .ToList());
    }
}
