using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Portal.ListChangeOrders;

internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        KaffDbContext database,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        List<Kaff.Domain.ChangeOrders.ChangeOrder> orders = await PortalChangeOrders.WithChildren(database).AsNoTracking()
            .Where(order => order.ProjectId == projectId && order.IsPriced)
            .OrderBy(order => order.Number)
            .ToListAsync(cancellationToken);

        return Results.Ok(orders.Where(PortalChangeOrders.IsVisible).Select(PortalChangeOrders.Describe).ToList());
    }
}
