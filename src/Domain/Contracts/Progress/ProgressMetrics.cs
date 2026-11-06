using Kaff.Domain.Boqs;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts.Billing;
using Kaff.Domain.Projects;

namespace Kaff.Domain.Contracts.Progress;

/*
 * The three registered progress metrics. A metric either measures from real evidence or fails; none returns
 * a made-up zero. A stub that returns "0% complete" reads as a real answer, which is why Design still fails
 * with BillingErrors.CalculatorNotImplemented: nothing stores stage completion for it to measure.
 */

/// <summary>
/// Lump Sum progress: work done against the signed contract value (spec.md §5.1). "Work done" is the executed
/// quantity priced at the frozen BOQ rate, not the certified value of issued extracts, so the bar moves with
/// the daily log. See <c>Rulings.LumpSumCompletionBasis</c>.
/// </summary>
public sealed class LumpSumProgressMetric : ProgressMetric<LumpSumProgressInput>
{
    public override ContractType ContractType => ContractType.LumpSum;

    public override Result<ProgressInput> InputFrom(Project project, ProgressEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        if (evidence.Boq is null)
        {
            return Result.Failure<ProgressInput>(BoqErrors.Required);
        }

        Result<LumpSumProgressInput> input = LumpSumProgressInput.FromBoq(evidence.Boq, evidence.Rulings.ContractValueSource);
        return input.IsSuccess ? Result.Success<ProgressInput>(input.Value) : Result.Failure<ProgressInput>(input.Error);
    }

    protected override Task<Result<ProgressReading>> MeasureCoreAsync(
        ProgressContext context,
        LumpSumProgressInput input,
        CancellationToken cancellationToken)
    {
        Money? contractValue = context.Project.ContractValue;
        if (contractValue is not { IsPositive: true })
        {
            return Task.FromResult(Result.Failure<ProgressReading>(BillingErrors.TermsMissing));
        }

        Percentage completion = Percentage.FromFraction(input.ExecutedValueToDate.Amount / contractValue.Value.Amount);
        return Task.FromResult(Result.Success(
            ProgressReading.FromPercentage(completion, input.ExecutedValueToDate, input.BabSegments)));
    }
}

/// <summary>
/// Cost Plus progress: cost-to-date plus supervision, and no percentage. spec.md §5.2. It returns
/// <see cref="ProgressReading.MonetaryOnly"/>, the only factory that cannot produce a percentage.
/// </summary>
public sealed class CostPlusProgressMetric : ProgressMetric<CostPlusProgressInput>
{
    public override ContractType ContractType => ContractType.CostPlus;

    public override Result<ProgressInput> InputFrom(Project project, ProgressEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(evidence);

        if (project.SupervisionRate is not { } rate)
        {
            return Result.Failure<ProgressInput>(BillingErrors.TermsMissing);
        }

        // Every cost counts as supervised: the per-line classification is not modelled yet. See
        // Rulings.CostPlusCostBasis.
        return Result.Success<ProgressInput>(new CostPlusProgressInput(evidence.CostToDate, evidence.CostToDate * rate.Fraction));
    }

    protected override Task<Result<ProgressReading>> MeasureCoreAsync(
        ProgressContext context,
        CostPlusProgressInput input,
        CancellationToken cancellationToken)
        => Task.FromResult(Result.Success(ProgressReading.MonetaryOnly(input.BillableCostToDate, input.SupervisionToDate)));
}

/// <summary>Design progress: the five fixed-weight stages. spec.md §5.3. Nothing stores stage completion, so it fails.</summary>
public sealed class DesignProgressMetric : ProgressMetric<DesignProgressInput>
{
    public override ContractType ContractType => ContractType.Design;

    public override Result<ProgressInput> InputFrom(Project project, ProgressEvidence evidence)
        => Result.Failure<ProgressInput>(BillingErrors.CalculatorNotImplemented);

    protected override Task<Result<ProgressReading>> MeasureCoreAsync(
        ProgressContext context,
        DesignProgressInput input,
        CancellationToken cancellationToken)
        => Task.FromResult(Result.Failure<ProgressReading>(BillingErrors.CalculatorNotImplemented));
}
