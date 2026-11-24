using Kaff.Domain.Boqs;
using Kaff.Domain.Common;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;

namespace Kaff.Domain.Accounting;

/// <summary>
/// A project's budget baseline: the signed BOQ's cost total plus the Owner's tolerance (spec.md section 6.8). KAFF-709.
/// </summary>
/// <remarks>
/// <b>Frozen by copy.</b> The baseline holds the cost total as a value, taken once. <see cref="SourceBoqId"/> is a plain
/// identifier kept for traceability and is not a foreign key, so nothing that later happens to the BOQ or the catalogue
/// reaches this figure (CLAUDE.md). There is no edit and no delete: "nothing else may edit it". The baseline rising by an
/// approved change order's cost is not built (R4-Q6: the cost impact of a priced change order is not defined).
/// </remarks>
public sealed class BudgetBaseline : Entity
{
    private BudgetBaseline()
    {
    }

    private BudgetBaseline(Guid projectId, Guid sourceBoqId, Money costTotal, Percentage tolerance, Guid setByUserId, DateTimeOffset setAt)
        : base(NewId())
    {
        ProjectId = projectId;
        SourceBoqId = sourceBoqId;
        CostTotal = costTotal;
        Tolerance = tolerance;
        SetByUserId = setByUserId;
        SetAt = setAt;
    }

    public Guid ProjectId { get; private set; }

    public Guid SourceBoqId { get; private set; }

    /// <summary>The signed BOQ's cost total, copied at the time the baseline was set.</summary>
    public Money CostTotal { get; private set; }

    public Percentage Tolerance { get; private set; }

    public Guid SetByUserId { get; private set; }

    public DateTimeOffset SetAt { get; private set; }

    /// <summary>The most committed money may reach before an alert: the baseline plus its tolerance.</summary>
    public Money Limit => Tolerance.Uplift(CostTotal);

    public static Result<BudgetBaseline> Set(
        Project project,
        Boq? boq,
        bool alreadySet,
        decimal? tolerancePercent,
        RulingSet rulings,
        Guid actor,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(rulings);

        if (alreadySet)
        {
            return Result.Failure<BudgetBaseline>(AccountingErrors.BaselineAlreadySet);
        }

        if (!project.IsSigned || boq is null || !boq.IsFrozen || boq.ProjectId != project.Id)
        {
            return Result.Failure<BudgetBaseline>(AccountingErrors.BaselineNeedsSignedBoq);
        }

        if (tolerancePercent is < 0m or > 100m)
        {
            return Result.Failure<BudgetBaseline>(AccountingErrors.ToleranceInvalid);
        }

        Result<Money> cost = CostTotalOf(boq, rulings.ContractValueSource);
        if (cost.IsFailure)
        {
            return Result.Failure<BudgetBaseline>(cost.Error);
        }

        Percentage tolerance = tolerancePercent is { } percent
            ? Percentage.FromPercent(percent)
            : rulings.BudgetTolerance.Tolerance;

        return Result.Success(new BudgetBaseline(project.Id, boq.Id, cost.Value, tolerance, actor, now));
    }

    /// <summary>The BOQ's cost total: each line's cost price times the same quantity and rounding the contract value uses.</summary>
    public static Result<Money> CostTotalOf(Boq boq, ContractValueSource rule)
    {
        ArgumentNullException.ThrowIfNull(boq);
        ArgumentNullException.ThrowIfNull(rule);

        if (boq.Lines.Count == 0)
        {
            return Result.Failure<Money>(BoqErrors.Empty);
        }

        Money total = Money.Zero;

        foreach (BoqLine line in boq.Lines)
        {
            decimal? quantity = line.QuantityFor(rule.QuantityBasis);

            if (quantity is null)
            {
                return Result.Failure<Money>(BoqErrors.LineQuantityMissing);
            }

            total += rule.LineTotal(line.CostPrice, quantity.Value);
        }

        return Result.Success(total);
    }
}
