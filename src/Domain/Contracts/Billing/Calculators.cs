using Kaff.Domain.Common;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;

namespace Kaff.Domain.Contracts.Billing;

/*
 * The three registered calculators (spec.md §5, acceptance figures in §15).
 *
 * Every business number comes from the Project's own terms (rates, set per project) or from RulingSet
 * (unruled defaults). Nothing is hard-coded here. A figure that has neither a spec line nor a ruling is
 * refused with a distinct error rather than guessed.
 */

/// <summary>Lump Sum billing. spec.md §5.1.</summary>
public sealed class LumpSumBillingCalculator(RulingSet rulings) : BillingCalculator<LumpSumBillingInput>
{
    private readonly RulingSet _rulings = rulings ?? throw new ArgumentNullException(nameof(rulings));

    public override ContractType ContractType => ContractType.LumpSum;

    protected override Task<Result<BillingResult>> CalculateCoreAsync(
        BillingContext context,
        LumpSumBillingInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Calculate(context, input));
    }

    private Result<BillingResult> Calculate(BillingContext context, LumpSumBillingInput input)
    {
        var project = context.Project;

        if (project.HoldRate is not { } holdRate
            || project.AdvanceRecoveryRate is not { } advanceRecoveryRate
            || project.MaterialAdvanceRate is not { } materialAdvanceRate)
        {
            return Result.Failure<BillingResult>(BillingErrors.TermsMissing);
        }

        Money periodWork = input.CumulativeCertifiedWork - input.PreviouslyCertifiedWork;
        if (periodWork.IsNegative)
        {
            return Result.Failure<BillingResult>(BillingErrors.NegativePeriodWork);
        }

        if (input.ChangeOrderValueThisPeriod.IsNegative)
        {
            return Result.Failure<BillingResult>(BillingErrors.NegativePeriodWork);
        }

        if (!input.DelayPenalty.IsZero && !project.DelayPenaltyEnabled)
        {
            return Result.Failure<BillingResult>(BillingErrors.DelayPenaltyNotEnabled);
        }

        DeductionRounding rounding = _rulings.DeductionRounding;

        Money work = rounding.Round(periodWork);

        // spec.md §5.1: change orders are billed in their own section at their own prices. Whether the hold and
        // the advance recovery are taken on them is unruled (Q102); the base comes from the ruling.
        Money changeOrders = rounding.Round(input.ChangeOrderValueThisPeriod);
        Money certified = work + changeOrders;
        Money deductionBase = _rulings.ChangeOrderBillingTreatment.BaseFor(work, changeOrders);
        Money hold = rounding.Deduct(deductionBase, holdRate);

        // spec.md §5.1: recovered "until the advance ledger reaches zero".
        Money advanceWanted = rounding.Deduct(deductionBase, advanceRecoveryRate);
        Money advanceLeft = input.ClientAdvanceOutstanding.IsPositive ? input.ClientAdvanceOutstanding : Money.Zero;
        Money advanceRecovery = advanceWanted > advanceLeft ? advanceLeft : advanceWanted;

        Money materialIssued = rounding.Round(materialAdvanceRate.Of(input.MaterialValueOnSiteThisPeriod));
        Money materialRecovery = rounding.Round(_rulings.MaterialAdvanceRecovery.RecoveryFor(
            input.MaterialInstalledThisPeriod, input.MaterialAdvanceOutstanding));
        Money penalty = rounding.Round(input.DelayPenalty);

        Money net = rounding.NetPayable(certified, [hold, advanceRecovery, materialRecovery, penalty]) + materialIssued;
        if (net.IsNegative)
        {
            return Result.Failure<BillingResult>(BillingErrors.NetPayableNegative);
        }

        var components = new List<BillingComponent>
        {
            Line(BillingComponentKind.WorkValue, BillingSign.Addition, work),
            Line(BillingComponentKind.ChangeOrderValue, BillingSign.Addition, changeOrders),
            Line(BillingComponentKind.MaterialAdvance, BillingSign.Addition, materialIssued),
            Line(BillingComponentKind.Hold, BillingSign.Deduction, hold),
            Line(BillingComponentKind.HoldToDate, BillingSign.Memo, input.HoldToDatePrevious + hold),
            Line(BillingComponentKind.AdvanceRecovery, BillingSign.Deduction, advanceRecovery),
            Line(BillingComponentKind.MaterialAdvanceRecovery, BillingSign.Deduction, materialRecovery),
            Line(BillingComponentKind.DelayPenalty, BillingSign.Deduction, penalty),
        };

        if (project.WithholdingCategory != WithholdingCategory.None)
        {
            Money basis = _rulings.WithholdingBase.BaseFor(new ExtractFigures(work, hold, net));
            Money withheld = rounding.Deduct(basis, WithholdingRates.For(project.WithholdingCategory));
            components.Add(Line(BillingComponentKind.TaxAtSource, BillingSign.Memo, withheld));
        }

        return Result.Success(new BillingResult(ContractType.LumpSum, certified, net, components));
    }

    private static BillingComponent Line(BillingComponentKind kind, BillingSign sign, Money amount) =>
        new(kind, sign, amount, $"billing.component.{kind}");
}

/// <summary>Cost Plus billing. spec.md §5.2.</summary>
public sealed class CostPlusBillingCalculator : BillingCalculator<CostPlusBillingInput>
{
    public override ContractType ContractType => ContractType.CostPlus;

    protected override Task<Result<BillingResult>> CalculateCoreAsync(
        BillingContext context,
        CostPlusBillingInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.Project.SupervisionRate is not { } supervision)
        {
            return Task.FromResult(Result.Failure<BillingResult>(BillingErrors.TermsMissing));
        }

        // Non-billable lines are never read: Kaff absorbs them, and that includes its own engineers' hours.
        Money supervisedCost = Money.Sum(input.Lines
            .Where(line => line.Classification == CostPlusClassification.Supervised)
            .Select(line => line.CostThisPeriod));
        Money markup = supervision.Of(supervisedCost);
        Money exemptCost = Money.Sum(input.Lines
            .Where(line => line.Classification == CostPlusClassification.Exempt)
            .Select(line => line.CostThisPeriod));

        Money invoice = supervisedCost + markup + exemptCost;

        BillingComponent[] components =
        [
            new(BillingComponentKind.WorkValue, BillingSign.Addition, supervisedCost, "billing.component.WorkValue"),
            new(BillingComponentKind.SupervisionFee, BillingSign.Addition, markup, "billing.component.SupervisionFee"),
            new(BillingComponentKind.CostAtCost, BillingSign.Addition, exemptCost, "billing.component.CostAtCost"),
        ];

        return Task.FromResult(Result.Success(new BillingResult(ContractType.CostPlus, invoice, invoice, components)));
    }
}

/// <summary>Design stage billing. spec.md §5.3.</summary>
public sealed class DesignBillingCalculator : BillingCalculator<DesignBillingInput>
{
    public override ContractType ContractType => ContractType.Design;

    protected override Task<Result<BillingResult>> CalculateCoreAsync(
        BillingContext context,
        DesignBillingInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.Project.ContractValue is not { } total)
        {
            return Task.FromResult(Result.Failure<BillingResult>(BillingErrors.TermsMissing));
        }

        if (!input.ClientApprovedDeliverable)
        {
            return Task.FromResult(Result.Failure<BillingResult>(BillingErrors.DeliverableNotApproved));
        }

        // Revision rounds beyond the agreed number bill a mini change order whose price spec.md does not
        // give. The stage fee is independent of it, so rounds are not read here (a question for Karim).
        Money fee = ContractDefaults.DesignStageWeights[input.Stage].Of(total);

        BillingComponent[] components =
            [new(BillingComponentKind.StageFee, BillingSign.Addition, fee, "billing.component.StageFee")];

        return Task.FromResult(Result.Success(new BillingResult(ContractType.Design, fee, fee, components)));
    }
}
