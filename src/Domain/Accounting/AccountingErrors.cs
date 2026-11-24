using Kaff.Domain.Common;

namespace Kaff.Domain.Accounting;

/// <summary>Error catalogue for the accounting layer: assets, accruals, prepayments, reports and the budget baseline (spec.md section 6.6, 6.8, 6.10).</summary>
public static class AccountingErrors
{
    /// <summary>An account passed to an accounting call is not of the type the call needs.</summary>
    public static readonly Error RequestNotFound =
        Error.NotFound("accounting.request_not_found", "errors.accounting.request_not_found");

    /// <summary>The request was already approved or rejected, so it cannot be decided again.</summary>
    public static readonly Error RequestNotWaiting =
        Error.Conflict("accounting.request_not_waiting", "errors.accounting.request_not_waiting");

    public static readonly Error AccountWrongType =
        Error.Validation("accounting.account_wrong_type", "errors.accounting.account_wrong_type");

    public static readonly Error DescriptionTooLong =
        Error.Validation("accounting.description_too_long", "errors.accounting.description_too_long");

    public static readonly Error MonthInvalid =
        Error.Validation("accounting.month_invalid", "errors.accounting.month_invalid");

    public static readonly Error MonthInFuture =
        Error.Conflict("accounting.month_in_future", "errors.accounting.month_in_future");

    public static readonly Error ReportRangeInvalid =
        Error.Validation("accounting.report_range_invalid", "errors.accounting.report_range_invalid");

    // ---- Accrual ----

    public static readonly Error AccrualNotFound =
        Error.NotFound("accounting.accrual_not_found", "errors.accounting.accrual_not_found");

    public static readonly Error AccrualAlreadyReleased =
        Error.Conflict("accounting.accrual_already_released", "errors.accounting.accrual_already_released");

    // ---- Prepayment ----

    public static readonly Error PrepaymentNotFound =
        Error.NotFound("accounting.prepayment_not_found", "errors.accounting.prepayment_not_found");

    public static readonly Error PrepaymentMonthsInvalid =
        Error.Validation("accounting.prepayment_months_invalid", "errors.accounting.prepayment_months_invalid");

    public static readonly Error PrepaymentFundingMismatch =
        Error.Conflict("accounting.prepayment_funding_mismatch", "errors.accounting.prepayment_funding_mismatch");

    public static readonly Error PrepaymentMonthOutsideSchedule =
        Error.Conflict("accounting.prepayment_month_outside_schedule", "errors.accounting.prepayment_month_outside_schedule");

    // ---- Asset register ----

    public static readonly Error AssetNotFound =
        Error.NotFound("accounting.asset_not_found", "errors.accounting.asset_not_found");

    public static readonly Error AssetNameRequired =
        Error.Validation("accounting.asset_name_required", "errors.accounting.asset_name_required");

    public static readonly Error AssetRateInvalid =
        Error.Validation("accounting.asset_rate_invalid", "errors.accounting.asset_rate_invalid");

    public static readonly Error AssetRateLocked =
        Error.Conflict("accounting.asset_rate_locked", "errors.accounting.asset_rate_locked");

    public static readonly Error AssetMonthBeforeStart =
        Error.Conflict("accounting.asset_month_before_start", "errors.accounting.asset_month_before_start");

    // ---- Budget baseline ----

    public static readonly Error BaselineNotSet =
        Error.NotFound("accounting.baseline_not_set", "errors.accounting.baseline_not_set");

    public static readonly Error BaselineAlreadySet =
        Error.Conflict("accounting.baseline_already_set", "errors.accounting.baseline_already_set");

    public static readonly Error BaselineNeedsSignedBoq =
        Error.Conflict("accounting.baseline_needs_signed_boq", "errors.accounting.baseline_needs_signed_boq");

    public static readonly Error ToleranceInvalid =
        Error.Validation("accounting.tolerance_invalid", "errors.accounting.tolerance_invalid");
}
