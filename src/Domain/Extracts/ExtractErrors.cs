using Kaff.Domain.Common;

namespace Kaff.Domain.Extracts;

/// <summary>Error catalogue for the extract chain (spec.md section 7).</summary>
public static class ExtractErrors
{
    public static readonly Error IllegalTransition =
        Error.Conflict("extract.illegal_transition", "errors.extract.illegal_transition");

    public static readonly Error ReasonRequired =
        Error.Validation("extract.reason_required", "errors.extract.reason_required");

    public static readonly Error ReasonTooLong =
        Error.Validation("extract.reason_too_long", "errors.extract.reason_too_long");

    public static readonly Error PeriodInvalid =
        Error.Validation("extract.period_invalid", "errors.extract.period_invalid");

    public static readonly Error QuantitiesInvalid =
        Error.Validation("extract.quantities_invalid", "errors.extract.quantities_invalid");

    public static readonly Error ApprovedPortionOutOfRange =
        Error.Validation("extract.approved_portion_out_of_range", "errors.extract.approved_portion_out_of_range");

    public static readonly Error HeldPortionNeedsNcr =
        Error.Validation("extract.held_portion_needs_ncr", "errors.extract.held_portion_needs_ncr");

    public static readonly Error NoHeldPortion =
        Error.Conflict("extract.no_held_portion", "errors.extract.no_held_portion");

    public static readonly Error HeldPortionAlreadyReleased =
        Error.Conflict("extract.held_portion_already_released", "errors.extract.held_portion_already_released");

    public static readonly Error ContractTypeNotSupported =
        Error.Conflict("extract.contract_type_not_supported", "errors.extract.contract_type_not_supported");

    public static readonly Error ProjectMismatch =
        Error.Conflict("extract.project_mismatch", "errors.extract.project_mismatch");

    /// <summary>
    /// The ruled accrual moment is a stage at which the extract's figures do not exist yet (the Technical Office
    /// gate), or one the chain cannot accrue at (Collected, which has its own postings).
    /// </summary>
    public static readonly Error AccrualMomentUnsupported =
        Error.Conflict("extract.accrual_moment_unsupported", "errors.extract.accrual_moment_unsupported");

    public static readonly Error ReversalPostingsRequired =
        Error.Conflict("extract.reversal_postings_required", "errors.extract.reversal_postings_required");

    public static readonly Error PostingNotFromThisExtract =
        Error.Conflict("extract.posting_not_from_this_extract", "errors.extract.posting_not_from_this_extract");
}
