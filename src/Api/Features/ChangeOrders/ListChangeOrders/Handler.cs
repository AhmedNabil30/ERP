using Kaff.Domain.ChangeOrders;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.ChangeOrders.ListChangeOrders;

/// <summary>One row of the list. Money-free on purpose.</summary>
public sealed record ChangeOrderSummary(Guid Id, int Number, string Title, ChangeOrderStatus Status, bool IsPriced);

internal static class Handler
{
    public static async Task<IResult> HandleAsync(Guid projectId, KaffDbContext database, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        List<ChangeOrderSummary> rows = await database.ChangeOrders
            .AsNoTracking()
            .Where(order => order.ProjectId == projectId)
            .OrderBy(order => order.Number)
            .Select(order => new ChangeOrderSummary(order.Id, order.Number, order.Title, order.Status, order.IsPriced))
            .ToListAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(rows);
    }
}
