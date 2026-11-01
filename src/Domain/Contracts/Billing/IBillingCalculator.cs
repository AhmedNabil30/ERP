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
    /// Kept for the progress metrics, which are still stubs. The three billing calculators no longer use it.
    /// A failure rather than a zero result: a metric that quietly returned zero would look like a business outcome.
    /// </summary>
    public static readonly Error CalculatorNotImplemented =
        Error.Conflict("billing.calculator_not_implemented", "errors.billing.calculator_not_implemented");

    /// <summary>The project is missing a term this contract type bills with (rate, area, contract value).</summary>
    public static readonly Error TermsMissing =
        Error.Conflict("billing.terms_missing", "errors.billing.terms_missing");

    /// <summary>Cumulative certified work is below what earlier extracts already certified.</summary>
    public static readonly Error NegativePeriodWork =
        Error.Validation("billing.negative_period_work", "errors.billing.negative_period_work");

    /// <summary>
    /// Change orders were supplied but no ruling says whether hold and advance recovery apply to them.
    /// Refused rather than guessed (CLAUDE.md: an invented rule is always plausible).
    /// </summary>
    public static readonly Error ChangeOrderTreatmentUnruled =
        Error.Conflict("billing.change_order_treatment_unruled", "errors.billing.change_order_treatment_unruled");

    /// <summary>A delay penalty was supplied on a project where the penalty is switched off.</summary>
    public static readonly Error DelayPenaltyNotEnabled =
        Error.Conflict("billing.delay_penalty_not_enabled", "errors.billing.delay_penalty_not_enabled");

    /// <summary>The deductions exceed the extract: nothing would be collected, and a negative collection is not a posting.</summary>
    public static readonly Error NetPayableNegative =
        Error.Conflict("billing.net_payable_negative", "errors.billing.net_payable_negative");

    /// <summary>spec.md §5.3: a design stage bills only when the client approves its deliverable.</summary>
    public static readonly Error DeliverableNotApproved =
        Error.Conflict("billing.deliverable_not_approved", "errors.billing.deliverable_not_approved");

    /// <summary>The extract has not reached the stage at which the ruling says its postings are written.</summary>
    public static readonly Error NotAtPostingMoment =
        Error.Conflict("billing.not_at_posting_moment", "errors.billing.not_at_posting_moment");

    /// <summary>The result carries a withholding amount but no tax-withheld-at-source account was supplied.</summary>
    public static readonly Error TaxAtSourceAccountRequired =
        Error.Conflict("billing.tax_at_source_account_required", "errors.billing.tax_at_source_account_required");
}
