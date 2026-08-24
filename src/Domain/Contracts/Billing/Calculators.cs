using Kaff.Domain.Common;

namespace Kaff.Domain.Contracts.Billing;

/*
 * The three registered calculators.
 *
 * Slice 0 delivers the seam: three implementations, one per contract type, resolvable by type and
 * unable to receive the wrong input. The arithmetic of spec.md §5 and the acceptance figures of
 * spec.md §15 are slice 5.
 *
 * Each returns BillingErrors.CalculatorNotImplemented rather than throwing or returning zero.
 * A thrown NotImplementedException would surface as a 500 and look like an outage; a zero result
 * would look like a business answer and could be approved. A distinct, translatable failure is the
 * only honest option for a stub that sits in the money path.
 */

/// <summary>Lump Sum billing. spec.md §5.1. Arithmetic pending — slice 5.</summary>
public sealed class LumpSumBillingCalculator : BillingCalculator<LumpSumBillingInput>
{
    public override ContractType ContractType => ContractType.LumpSum;

    protected override Task<Result<BillingResult>> CalculateCoreAsync(
        BillingContext context,
        LumpSumBillingInput input,
        CancellationToken cancellationToken)
        => Task.FromResult(Result.Failure<BillingResult>(BillingErrors.CalculatorNotImplemented));
}

/// <summary>Cost Plus billing. spec.md §5.2. Arithmetic pending — slice 5.</summary>
public sealed class CostPlusBillingCalculator : BillingCalculator<CostPlusBillingInput>
{
    public override ContractType ContractType => ContractType.CostPlus;

    protected override Task<Result<BillingResult>> CalculateCoreAsync(
        BillingContext context,
        CostPlusBillingInput input,
        CancellationToken cancellationToken)
        => Task.FromResult(Result.Failure<BillingResult>(BillingErrors.CalculatorNotImplemented));
}

/// <summary>Design stage billing. spec.md §5.3. Arithmetic pending — slice 5.</summary>
public sealed class DesignBillingCalculator : BillingCalculator<DesignBillingInput>
{
    public override ContractType ContractType => ContractType.Design;

    protected override Task<Result<BillingResult>> CalculateCoreAsync(
        BillingContext context,
        DesignBillingInput input,
        CancellationToken cancellationToken)
        => Task.FromResult(Result.Failure<BillingResult>(BillingErrors.CalculatorNotImplemented));
}
