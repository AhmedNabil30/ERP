using System.Text.Json.Serialization;
using Kaff.Api.Common.Results;
using Kaff.Api.Features.ChangeOrders;
using Kaff.Api.Features.Portal.ListChangeOrders;
using Kaff.Domain.ChangeOrders;
using Kaff.Domain.Common;
using Kaff.Domain.Projects;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Portal.GetChangeOrder;

/// <summary>
/// One change order as the client reads it: the list shape plus when it reached him and what the contract value is without it and with it.
/// Both values are two-decimal strings and absent while the project has no contract value. No cost field.
/// </summary>
/// <param name="SubmittedAt">When technical review passed it on to the client's gate. Absent if the history holds no such step.</param>
/// <param name="ContractValueBefore">The signed value plus every other approved change order.</param>
/// <param name="ContractValueAfter">The value before plus this change order's total.</param>
public sealed record PortalChangeOrderDetail(
    Guid Id,
    int Number,
    string Title,
    string Status,
    string Total,
    bool CanDecide,
    IReadOnlyList<PortalChangeOrderLine> Lines,
    DateTimeOffset? SubmittedAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ContractValueBefore,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ContractValueAfter);

internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        Guid id,
        KaffDbContext database,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        ChangeOrder? order = await PortalChangeOrders.WithChildren(database).AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.ProjectId == projectId, cancellationToken);
        if (order is null || !PortalChangeOrders.IsVisible(order))
        {
            return ResultExtensions.Problem(ChangeOrderErrors.NotFound);
        }

        Project project = await database.Projects.AsNoTracking().FirstAsync(candidate => candidate.Id == projectId, cancellationToken);
        Money others = await ChangeOrderQueries.ApprovedTotalAsync(database, projectId, cancellationToken);
        if (order.Status == ChangeOrderStatus.Approved)
        {
            others -= order.Total;
        }

        Money? before = project.RevisedContractValue(others);
        PortalChangeOrder shape = PortalChangeOrders.Describe(order);

        return Results.Ok(new PortalChangeOrderDetail(
            shape.Id,
            shape.Number,
            shape.Title,
            shape.Status,
            shape.Total,
            shape.CanDecide,
            shape.Lines,
            order.GateHistory.Where(gate => gate.Gate == ChangeOrderGate.TechnicalReview && gate.Outcome == ChangeOrderGateOutcome.Passed)
                .Select(gate => (DateTimeOffset?)gate.OccurredAt).Max(),
            PortalFormat.Amount(before),
            PortalFormat.Amount(before is { } present ? present + order.Total : null)));
    }
}
