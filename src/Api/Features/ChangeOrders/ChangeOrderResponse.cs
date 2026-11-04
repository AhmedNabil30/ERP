using Kaff.Domain.Authorization;
using Kaff.Domain.ChangeOrders;
using Kaff.Domain.Common;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;

namespace Kaff.Api.Features.ChangeOrders;

public sealed record ChangeOrderLineResponse(int LineNumber, string Description, string Unit, decimal Quantity, Money UnitPrice, Money Total);

public sealed record ChangeOrderGateRecordResponse(
    ChangeOrderGate Gate, ChangeOrderGateOutcome Outcome, Guid ActorUserId, DateTimeOffset OccurredAt, string? Reason);

/// <summary>
/// What this caller may do to this change order right now. A control whose flag is false is left out of the screen,
/// not shown disabled. Each flag is role and assignment (the permission, evaluated for the route project) AND the
/// state machine AND, at a decision gate, <see cref="ChangeOrder.EnsureMayDecide"/> over the stored gate actors.
/// </summary>
public sealed record ChangeOrderActions(
    bool Submit,
    bool ApproveTechnicalReview,
    bool RejectAtTechnicalReview,
    bool RecordClientApproval,
    bool RejectAtClientApproval,
    bool ApproveAsOwner,
    bool RejectAtOwner,
    bool Withdraw,
    bool Log,
    bool CallerMayDecide);

/// <param name="Lines">Null when the caller may not see money.</param>
/// <param name="Total">The change order price, derived from its lines. Null when the caller may not see money.</param>
public sealed record ChangeOrderResponse(
    Guid Id,
    Guid ProjectId,
    int Number,
    string Title,
    ChangeOrderStatus Status,
    bool IsPriced,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ApprovedAt,
    IReadOnlyList<ChangeOrderGateRecordResponse> GateHistory,
    ChangeOrderActions Actions,
    IReadOnlyList<ChangeOrderLineResponse>? Lines,
    Money? Total);

/// <summary>Who is looking: their id and which of the change-order permissions they hold on this project.</summary>
internal sealed record ChangeOrderViewer(Guid UserId, IReadOnlySet<Permission> Held)
{
    /// <summary>
    /// A change order price is money. The holders of its four permissions see it; a site engineer, who approves
    /// nothing financial (spec.md section 9), does not.
    /// </summary>
    public bool MaySeeMoney => Held.Count > 0;
}

internal static class ChangeOrderView
{
    public static readonly Permission[] Permissions =
    [
        Permission.ChangeOrderPrepare,
        Permission.ChangeOrderReview,
        Permission.ChangeOrderClientApprovalRecord,
        Permission.ChangeOrderApprove,
    ];

    public static ChangeOrderResponse Build(ChangeOrder order, Project project, RulingSet rulings, ChangeOrderViewer viewer)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(rulings);
        ArgumentNullException.ThrowIfNull(viewer);

        bool seesMoney = viewer.MaySeeMoney;

        return new ChangeOrderResponse(
            order.Id,
            order.ProjectId,
            order.Number,
            order.Title,
            order.Status,
            order.IsPriced,
            order.CreatedByUserId,
            order.CreatedAt,
            order.ApprovedAt,
            [.. order.GateHistory.Select(record => new ChangeOrderGateRecordResponse(
                record.Gate, record.Outcome, record.ActorUserId, record.OccurredAt, record.Reason))],
            ActionsFor(order, project, rulings, viewer),
            seesMoney
                ? [.. order.Lines.OrderBy(line => line.LineNumber).Select(line => new ChangeOrderLineResponse(
                    line.LineNumber, line.Description, line.Unit, line.Quantity, line.UnitPrice, line.Total))]
                : null,
            seesMoney ? order.Total : null);
    }

    private static ChangeOrderActions ActionsFor(ChangeOrder order, Project project, RulingSet rulings, ChangeOrderViewer viewer)
    {
        bool mayDecide = order.EnsureMayDecide(viewer.UserId).IsSuccess;
        ChangeOrderStatus status = order.Status;

        bool Holds(Permission permission) => viewer.Held.Contains(permission);
        bool Can(ChangeOrderStatus target) => order.MayMoveTo(target);
        bool projectAllows = rulings.ChangeOrderProjectStates.Allows(project.Status);

        return new ChangeOrderActions(
            Submit: Holds(Permission.ChangeOrderPrepare) && Can(ChangeOrderStatus.TechnicalReview),
            ApproveTechnicalReview: Holds(Permission.ChangeOrderReview) && Can(ChangeOrderStatus.ClientApproval) && mayDecide,
            RejectAtTechnicalReview: Holds(Permission.ChangeOrderReview) && status == ChangeOrderStatus.TechnicalReview && mayDecide,
            RecordClientApproval: Holds(Permission.ChangeOrderClientApprovalRecord) && Can(ChangeOrderStatus.OwnerApproval) && mayDecide,
            RejectAtClientApproval: Holds(Permission.ChangeOrderClientApprovalRecord) && status == ChangeOrderStatus.ClientApproval && mayDecide,
            ApproveAsOwner: Holds(Permission.ChangeOrderApprove) && Can(ChangeOrderStatus.Approved) && mayDecide && projectAllows,
            RejectAtOwner: Holds(Permission.ChangeOrderApprove) && status == ChangeOrderStatus.OwnerApproval && mayDecide,
            Withdraw: Holds(Permission.ChangeOrderPrepare) && Can(ChangeOrderStatus.Withdrawn),
            Log: Holds(Permission.ChangeOrderPrepare) && Can(ChangeOrderStatus.Logged),
            CallerMayDecide: mayDecide);
    }
}
