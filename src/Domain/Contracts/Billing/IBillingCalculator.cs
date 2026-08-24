using Kaff.Domain.Common;

namespace Kaff.Domain.Contracts.Billing;

/// <summary>
/// Turns a period's evidence into a billable result for one contract type.
/// </summary>
/// <remarks>
/// One of the two seams through which a contract type is allowed to differ (CLAUDE.md). Everything
/// else — the project entity, the treasury, the approval chain, the audit trail — is shared.
/// </remarks>
public interface IBillingCalculator
{
    /// <summary>The type this calculator serves. Used for dispatch; unique across implementations.</summary>
    ContractType ContractType { get; }

    Task<Result<BillingResult>> CalculateAsync(BillingContext context, CancellationToken cancellationToken);
}

/// <summary>
/// Base that performs the type check and the cast, so implementations never see the wrong input.
/// </summary>
/// <typeparam name="TInput">The input this calculator understands.</typeparam>
public abstract class BillingCalculator<TInput> : IBillingCalculator
    where TInput : BillingInput
{
    public abstract ContractType ContractType { get; }

    public Task<Result<BillingResult>> CalculateAsync(BillingContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Project.ContractType != ContractType)
        {
            return Task.FromResult(Result.Failure<BillingResult>(BillingErrors.ContractTypeMismatch));
        }

        if (context.Input is not TInput typedInput)
        {
            return Task.FromResult(Result.Failure<BillingResult>(BillingErrors.BillingInputMismatch));
        }

        return CalculateCoreAsync(context, typedInput, cancellationToken);
    }

    protected abstract Task<Result<BillingResult>> CalculateCoreAsync(
        BillingContext context,
        TInput input,
        CancellationToken cancellationToken);
}

/// <summary>Errors raised by billing dispatch and by calculators.</summary>
public static class BillingErrors
{
    public static readonly Error ContractTypeMismatch =
        Error.Conflict("billing.contract_type_mismatch", "errors.billing.contract_type_mismatch");

    public static readonly Error BillingInputMismatch =
        Error.Conflict("billing.input_mismatch", "errors.billing.input_mismatch");

    public static readonly Error ProgressInputMismatch =
        Error.Conflict("billing.progress_input_mismatch", "errors.billing.progress_input_mismatch");

    public static readonly Error NoCalculatorRegistered =
        Error.Conflict("billing.no_calculator_registered", "errors.billing.no_calculator_registered");

    public static readonly Error NoProgressMetricRegistered =
        Error.Conflict("billing.no_progress_metric_registered", "errors.billing.no_progress_metric_registered");

    /// <summary>
    /// The calculator is registered and dispatch works, but the arithmetic of spec.md §5 has not been
    /// written yet. Slice 0 builds the seam; slice 5 builds the money.
    ///
    /// This is deliberately a failure rather than a zero result. A calculator that quietly returned
    /// zero would issue an extract for nothing and look like a business outcome.
    /// </summary>
    public static readonly Error CalculatorNotImplemented =
        Error.Conflict("billing.calculator_not_implemented", "errors.billing.calculator_not_implemented");
}
