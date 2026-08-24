using Kaff.Domain.Common;
using Kaff.Domain.Contracts.Billing;

namespace Kaff.Domain.Contracts.Progress;

/*
 * The three registered progress metrics. Seam only; measurement logic is slice 5.
 * They fail with BillingErrors.CalculatorNotImplemented for the same reason the calculators do —
 * a stub that returns "0% complete" reads as a real answer.
 */

/// <summary>Lump Sum progress: certified value against contract value. spec.md §5.1.</summary>
public sealed class LumpSumProgressMetric : ProgressMetric<LumpSumProgressInput>
{
    public override ContractType ContractType => ContractType.LumpSum;

    protected override Task<Result<ProgressReading>> MeasureCoreAsync(
        ProgressContext context,
        LumpSumProgressInput input,
        CancellationToken cancellationToken)
        => Task.FromResult(Result.Failure<ProgressReading>(BillingErrors.CalculatorNotImplemented));
}

/// <summary>
/// Cost Plus progress: cost-to-date plus supervision, and no percentage. spec.md §5.2.
/// When implemented, this must return <see cref="ProgressReading.MonetaryOnly"/> — the only factory
/// that cannot produce a percentage.
/// </summary>
public sealed class CostPlusProgressMetric : ProgressMetric<CostPlusProgressInput>
{
    public override ContractType ContractType => ContractType.CostPlus;

    protected override Task<Result<ProgressReading>> MeasureCoreAsync(
        ProgressContext context,
        CostPlusProgressInput input,
        CancellationToken cancellationToken)
        => Task.FromResult(Result.Failure<ProgressReading>(BillingErrors.CalculatorNotImplemented));
}

/// <summary>Design progress: the five fixed-weight stages. spec.md §5.3.</summary>
public sealed class DesignProgressMetric : ProgressMetric<DesignProgressInput>
{
    public override ContractType ContractType => ContractType.Design;

    protected override Task<Result<ProgressReading>> MeasureCoreAsync(
        ProgressContext context,
        DesignProgressInput input,
        CancellationToken cancellationToken)
        => Task.FromResult(Result.Failure<ProgressReading>(BillingErrors.CalculatorNotImplemented));
}
