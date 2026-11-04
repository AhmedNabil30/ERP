using System.Collections.Frozen;
using Kaff.Domain.Authorization;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;

namespace Kaff.Domain.ChangeOrders;

/// <summary>One line a caller proposes for a priced change order. Copied onto the change order, never a key.</summary>
public sealed record ChangeOrderLineDraft(string Description, string Unit, decimal Quantity, Money UnitPrice);

/// <summary>
/// One priced line of a change order. Holds its own values (description, unit, quantity, unit price and the
/// rounded total), so a later catalogue or BOQ price change cannot reach it. Immutable once created.
/// </summary>
public sealed class ChangeOrderLine : Entity
{
    public const int MaxDescriptionLength = 500;
    public const int MaxUnitLength = 32;

    private ChangeOrderLine()
    {
    }

    internal ChangeOrderLine(
        Guid changeOrderId, int lineNumber, string description, string unit, decimal quantity, Money unitPrice, Money total)
        : base(NewId())
    {
        ChangeOrderId = changeOrderId;
        LineNumber = lineNumber;
        Description = description;
        Unit = unit;
        Quantity = quantity;
        UnitPrice = unitPrice;
        Total = total;
    }

    public Guid ChangeOrderId { get; private set; }

    public int LineNumber { get; private set; }

    public string Description { get; private set; } = null!;

    public string Unit { get; private set; } = null!;

    public decimal Quantity { get; private set; }

    public Money UnitPrice { get; private set; }

    /// <summary>Quantity times unit price, rounded as a BOQ line total is.</summary>
    public Money Total { get; private set; }
}

/// <summary>
/// One act at one gate of a change order's chain: who, when, and for a rejection why. Append-only and stored on the
/// change order, because the chain's separation-of-duties rule reads it.
/// </summary>
public sealed class ChangeOrderGateRecord : Entity
{
    private ChangeOrderGateRecord()
    {
    }

    internal ChangeOrderGateRecord(
        Guid changeOrderId,
        ChangeOrderGate gate,
        ChangeOrderGateOutcome outcome,
        Guid actorUserId,
        DateTimeOffset occurredAt,
        string? reason)
        : base(NewId())
    {
        ChangeOrderId = changeOrderId;
        Gate = gate;
        Outcome = outcome;
        ActorUserId = actorUserId;
        OccurredAt = occurredAt;
        Reason = reason;
    }

    public Guid ChangeOrderId { get; private set; }

    public ChangeOrderGate Gate { get; private set; }

    public ChangeOrderGateOutcome Outcome { get; private set; }

    public Guid ActorUserId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>Required and non-blank when <see cref="Outcome"/> is <see cref="ChangeOrderGateOutcome.Rejected"/>.</summary>
    public string? Reason { get; private set; }
}

/// <summary>
/// A change order (أمر تغيير), spec.md section 13. A Lump Sum change order is priced and runs the four-gate chain;
/// a Cost Plus or Design one is only logged. One entity for all three contract types: the type decides only
/// whether it is priced. Approving a priced change order raises the contract value, which stays derived (the
/// signed BOQ total plus the approved change orders); nothing here touches a BOQ line.
/// </summary>
/// <remarks>
/// Every step validates first and mutates last, so a refused step leaves the change order untouched. The stored
/// gate history is the rule's input: <see cref="EnsureMayDecide"/> reads it, and a screen calls the same method to
/// leave a control out for a user who may not use it.
/// </remarks>
public sealed class ChangeOrder : Entity
{
    public const int MaxTitleLength = 200;

    private static readonly FrozenSet<(ChangeOrderStatus From, ChangeOrderStatus To)> LegalTransitions =
        new HashSet<(ChangeOrderStatus, ChangeOrderStatus)>
        {
            (ChangeOrderStatus.Draft, ChangeOrderStatus.TechnicalReview),
            (ChangeOrderStatus.TechnicalReview, ChangeOrderStatus.ClientApproval),
            (ChangeOrderStatus.ClientApproval, ChangeOrderStatus.OwnerApproval),
            (ChangeOrderStatus.OwnerApproval, ChangeOrderStatus.Approved),

            // spec.md section 13: a rejection moves the change order to Rejected, from a gate where it waits.
            (ChangeOrderStatus.TechnicalReview, ChangeOrderStatus.Rejected),
            (ChangeOrderStatus.ClientApproval, ChangeOrderStatus.Rejected),
            (ChangeOrderStatus.OwnerApproval, ChangeOrderStatus.Rejected),

            (ChangeOrderStatus.Draft, ChangeOrderStatus.Withdrawn),
            (ChangeOrderStatus.TechnicalReview, ChangeOrderStatus.Withdrawn),
            (ChangeOrderStatus.ClientApproval, ChangeOrderStatus.Withdrawn),
            (ChangeOrderStatus.OwnerApproval, ChangeOrderStatus.Withdrawn),

            // Cost Plus and Design: documentary and unpriced.
            (ChangeOrderStatus.Draft, ChangeOrderStatus.Logged),
        }.ToFrozenSet();

    private readonly List<ChangeOrderLine> _lines = [];
    private readonly List<ChangeOrderGateRecord> _gateHistory = [];

    private ChangeOrder()
    {
    }

    private ChangeOrder(
        Guid projectId, int number, string title, bool isPriced, Guid createdByUserId, DateTimeOffset createdAt)
        : base(NewId())
    {
        ProjectId = projectId;
        Number = number;
        Title = title;
        IsPriced = isPriced;
        CreatedByUserId = createdByUserId;
        CreatedAt = createdAt;
        Status = ChangeOrderStatus.Draft;
        _gateHistory.Add(new ChangeOrderGateRecord(
            Id, ChangeOrderGate.Created, ChangeOrderGateOutcome.Passed, createdByUserId, createdAt, null));
    }

    public Guid ProjectId { get; private set; }

    /// <summary>Sequence number on the project: 1, 2, 3 (unique per project).</summary>
    public int Number { get; private set; }

    public string Title { get; private set; } = null!;

    public ChangeOrderStatus Status { get; private set; }

    /// <summary>True for a Lump Sum change order. Cost Plus and Design change orders carry no price.</summary>
    public bool IsPriced { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ApprovedAt { get; private set; }

    public IReadOnlyList<ChangeOrderLine> Lines => _lines;

    public IReadOnlyList<ChangeOrderGateRecord> GateHistory => _gateHistory;

    /// <summary>
    /// The change order's price: the sum of its line totals. Derived, never stored. Zero for an unpriced change
    /// order, which has no lines.
    /// </summary>
    public Money Total => Money.Sum(_lines.Select(line => line.Total));

    public static bool IsLegalTransition(ChangeOrderStatus from, ChangeOrderStatus to) => LegalTransitions.Contains((from, to));

    public static Result<ChangeOrder> Create(
        Project project,
        int number,
        string? title,
        IReadOnlyList<ChangeOrderLineDraft> lines,
        RulingSet rulings,
        Guid createdByUserId,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(rulings);

        if (!rulings.ChangeOrderProjectStates.Allows(project.Status))
        {
            return Result.Failure<ChangeOrder>(ChangeOrderErrors.ProjectStateNotAllowed);
        }

        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > MaxTitleLength)
        {
            return Result.Failure<ChangeOrder>(ChangeOrderErrors.TitleRequired);
        }

        bool priced = project.ContractType == ContractType.LumpSum;

        if (!priced && lines.Count > 0)
        {
            return Result.Failure<ChangeOrder>(ChangeOrderErrors.LinesNotAllowed);
        }

        if (priced && lines.Count == 0)
        {
            return Result.Failure<ChangeOrder>(ChangeOrderErrors.LinesRequired);
        }

        var order = new ChangeOrder(project.Id, number, title.Trim(), priced, createdByUserId, now);

        foreach (ChangeOrderLineDraft draft in lines)
        {
            if (string.IsNullOrWhiteSpace(draft.Description) || draft.Description.Trim().Length > ChangeOrderLine.MaxDescriptionLength
                || string.IsNullOrWhiteSpace(draft.Unit) || draft.Unit.Trim().Length > ChangeOrderLine.MaxUnitLength
                || draft.Quantity <= 0m || !draft.UnitPrice.IsPositive)
            {
                return Result.Failure<ChangeOrder>(ChangeOrderErrors.LineInvalid);
            }

            order._lines.Add(new ChangeOrderLine(
                order.Id, order._lines.Count + 1, draft.Description.Trim(), draft.Unit.Trim(), draft.Quantity, draft.UnitPrice,
                rulings.ContractValueSource.LineTotal(draft.UnitPrice, draft.Quantity)));
        }

        return Result.Success(order);
    }

    /// <summary>
    /// Who has already acted on this change order, creator included. A rejection ends it, so there is no second
    /// cycle: whoever acted earlier may not act at a later gate.
    /// </summary>
    public IReadOnlyCollection<Guid> PriorActors()
    {
        HashSet<Guid> actors = [CreatedByUserId];
        foreach (ChangeOrderGateRecord record in _gateHistory)
        {
            actors.Add(record.ActorUserId);
        }

        return actors;
    }

    /// <summary>
    /// Answers, before anyone clicks, whether this user may decide at a gate (spec.md section 9: nobody creates
    /// and approves the same movement). The gate methods call it too, so the screen and the refusal agree.
    /// </summary>
    public Result EnsureMayDecide(Guid userId) => SeparationOfDuties.EnsureDifferentActor(PriorActors(), userId);

    /// <summary>Draft to TechnicalReview.</summary>
    public Result Submit(Guid actorUserId, DateTimeOffset now)
    {
        Result move = CanMoveTo(ChangeOrderStatus.TechnicalReview);
        if (move.IsFailure)
        {
            return move;
        }

        Apply(ChangeOrderStatus.TechnicalReview, ChangeOrderGate.Submitted, ChangeOrderGateOutcome.Passed, actorUserId, now, null);
        return Result.Success();
    }

    /// <summary>TechnicalReview to ClientApproval. A different person from everyone who acted so far.</summary>
    public Result ApproveTechnicalReview(Guid actorUserId, DateTimeOffset now) =>
        Decide(ChangeOrderStatus.ClientApproval, ChangeOrderGate.TechnicalReview, actorUserId, now);

    /// <summary>ClientApproval to OwnerApproval. Records that the client approved; how is Q103.</summary>
    public Result RecordClientApproval(Guid actorUserId, DateTimeOffset now) =>
        Decide(ChangeOrderStatus.OwnerApproval, ChangeOrderGate.ClientApproval, actorUserId, now);

    /// <summary>OwnerApproval to Approved. The contract value now includes this change order.</summary>
    public Result OwnerApprove(Guid actorUserId, Project project, RulingSet rulings, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(rulings);

        Result gate = Result.FirstFailureOrSuccess(
            CanMoveTo(ChangeOrderStatus.Approved), EnsureMayDecide(actorUserId), CheckProject(project, rulings));
        if (gate.IsFailure)
        {
            return gate;
        }

        Apply(ChangeOrderStatus.Approved, ChangeOrderGate.Owner, ChangeOrderGateOutcome.Passed, actorUserId, now, null);
        ApprovedAt = now;
        return Result.Success();
    }

    /// <summary>
    /// Rejects at the gate the change order waits at. It ends at Rejected with a stored, non-blank reason
    /// (spec.md section 13, CLAUDE.md).
    /// </summary>
    public Result Reject(Guid actorUserId, string? reason, DateTimeOffset now)
    {
        Result gate = Result.FirstFailureOrSuccess(CanMoveTo(ChangeOrderStatus.Rejected), EnsureMayDecide(actorUserId));
        if (gate.IsFailure)
        {
            return gate;
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure(ChangeOrderErrors.ReasonRequired);
        }

        if (reason.Trim().Length > Project.MaxReasonLength)
        {
            return Result.Failure(ChangeOrderErrors.ReasonTooLong);
        }

        ChangeOrderGate at = Status switch
        {
            ChangeOrderStatus.TechnicalReview => ChangeOrderGate.TechnicalReview,
            ChangeOrderStatus.ClientApproval => ChangeOrderGate.ClientApproval,
            _ => ChangeOrderGate.Owner,
        };

        Apply(ChangeOrderStatus.Rejected, at, ChangeOrderGateOutcome.Rejected, actorUserId, now, reason.Trim());
        return Result.Success();
    }

    /// <summary>Takes the change order back from any non-terminal state before approval.</summary>
    public Result Withdraw(Guid actorUserId, DateTimeOffset now)
    {
        Result move = CanMoveTo(ChangeOrderStatus.Withdrawn);
        if (move.IsFailure)
        {
            return move;
        }

        Apply(ChangeOrderStatus.Withdrawn, ChangeOrderGate.Withdrawn, ChangeOrderGateOutcome.Passed, actorUserId, now, null);
        return Result.Success();
    }

    /// <summary>Draft to Logged. Cost Plus and Design only: documentary, unpriced, moves no money.</summary>
    public Result Log(Guid actorUserId, DateTimeOffset now)
    {
        Result move = CanMoveTo(ChangeOrderStatus.Logged);
        if (move.IsFailure)
        {
            return move;
        }

        Apply(ChangeOrderStatus.Logged, ChangeOrderGate.Logged, ChangeOrderGateOutcome.Passed, actorUserId, now, null);
        return Result.Success();
    }

    private Result Decide(ChangeOrderStatus target, ChangeOrderGate gate, Guid actorUserId, DateTimeOffset now)
    {
        Result allowed = Result.FirstFailureOrSuccess(CanMoveTo(target), EnsureMayDecide(actorUserId));
        if (allowed.IsFailure)
        {
            return allowed;
        }

        Apply(target, gate, ChangeOrderGateOutcome.Passed, actorUserId, now, null);
        return Result.Success();
    }

    private Result CheckProject(Project project, RulingSet rulings)
    {
        if (project.Id != ProjectId)
        {
            return Result.Failure(ChangeOrderErrors.NotFound);
        }

        return rulings.ChangeOrderProjectStates.Allows(project.Status)
            ? Result.Success()
            : Result.Failure(ChangeOrderErrors.ProjectStateNotAllowed);
    }

    /// <summary>
    /// A transition is legal when the set allows it and the type does: an unpriced change order only logs or is
    /// withdrawn, a priced one never logs.
    /// </summary>
    private Result CanMoveTo(ChangeOrderStatus target)
    {
        if (!LegalTransitions.Contains((Status, target)))
        {
            return Result.Failure(ChangeOrderErrors.IllegalTransition);
        }

        bool typeAllows = IsPriced
            ? target != ChangeOrderStatus.Logged
            : target is ChangeOrderStatus.Logged or ChangeOrderStatus.Withdrawn;
        return typeAllows ? Result.Success() : Result.Failure(ChangeOrderErrors.IllegalTransition);
    }

    /// <summary>True when the machine and the contract type allow a move to <paramref name="target"/> from here.</summary>
    public bool MayMoveTo(ChangeOrderStatus target) => CanMoveTo(target).IsSuccess;

    private void Apply(
        ChangeOrderStatus target,
        ChangeOrderGate gate,
        ChangeOrderGateOutcome outcome,
        Guid actorUserId,
        DateTimeOffset now,
        string? reason)
    {
        Status = target;
        _gateHistory.Add(new ChangeOrderGateRecord(Id, gate, outcome, actorUserId, now, reason));
    }
}
