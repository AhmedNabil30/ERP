using Kaff.Api.Common.Results;
using Kaff.Api.Features.Portal.ListChangeOrders;
using Kaff.Domain.ChangeOrders;
using Kaff.Domain.Common;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Portal;

/// <summary>What the portal's three change-order routes share: who may see which change order, its shape, and the decision step.</summary>
internal static class PortalChangeOrders
{
    public static IQueryable<ChangeOrder> WithChildren(KaffDbContext database) =>
        database.ChangeOrders.Include(order => order.Lines).Include(order => order.GateHistory);

    /// <summary>
    /// R5-Q3 and R5-Q14 defaults: a priced change order from <see cref="ChangeOrderStatus.ClientApproval"/> onward, plus a rejected one
    /// only when it had reached the client's gate. Draft, technical review, logged (unpriced) and withdrawn ones never show.
    /// </summary>
    public static bool IsVisible(ChangeOrder order) =>
        order.IsPriced
        && (order.Status is ChangeOrderStatus.ClientApproval or ChangeOrderStatus.OwnerApproval or ChangeOrderStatus.Approved
            || (order.Status == ChangeOrderStatus.Rejected
                && order.GateHistory.Any(gate => gate.Gate == ChangeOrderGate.ClientApproval)));

    public static PortalChangeOrder Describe(ChangeOrder order) => new(
        order.Id,
        order.Number,
        order.Title,
        order.Status.ToString(),
        PortalFormat.Amount(order.Total),
        order.Status == ChangeOrderStatus.ClientApproval,
        [.. order.Lines.OrderBy(line => line.LineNumber).Select(line => new PortalChangeOrderLine(
            line.LineNumber,
            line.Description,
            line.Unit,
            line.Quantity,
            PortalFormat.Amount(line.UnitPrice),
            PortalFormat.Amount(line.Total)))]);

    /// <summary>
    /// Runs one client decision under a row lock, as the staff change-order steps do, and answers with the portal shape. A change order the
    /// client may not see is a 404, whatever its real state, so the answer does not reveal Kaff's internal steps.
    /// </summary>
    public static async Task<IResult> DecideAsync(
        Guid projectId,
        Guid id,
        KaffDbContext database,
        Func<ChangeOrder, Result> step,
        CancellationToken cancellationToken)
    {
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await database.Database.BeginTransactionAsync(cancellationToken);

        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM change_orders WHERE id = {id} AND project_id = {projectId} FOR UPDATE", cancellationToken);

        ChangeOrder? order = await WithChildren(database)
            .FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.ProjectId == projectId, cancellationToken);

        if (order is null || !IsVisible(order))
        {
            return ResultExtensions.Problem(ChangeOrderErrors.NotFound);
        }

        Result stepped = step(order);
        if (stepped.IsFailure)
        {
            return ResultExtensions.Problem(stepped.Error);
        }

        Error? refused = await GuardedSave.SaveAsync(database, cancellationToken);
        if (refused is not null)
        {
            return ResultExtensions.Problem(refused);
        }

        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(Describe(order));
    }
}
