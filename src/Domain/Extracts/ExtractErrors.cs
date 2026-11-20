using Kaff.Domain.Common;

namespace Kaff.Domain.Extracts;

/// <summary>Error catalogue for the extract chain (spec.md section 7).</summary>
public static class ExtractErrors
{
    /// <summary>An approved change order is billed on one extract only (ruling ChangeOrderBillingBasis).</summary>
    public static readonly Error ChangeOrderAlreadyBilled =
        Error.Conflict("extract.change_order_already_billed", "errors.extract.change_order_already_billed");

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

    /// <summary>From handover on, the Technical Office approves an extract whole or rejects it (D-258 item 1).</summary>
    public static readonly Error SplitAfterHandover =
        Error.Conflict("extract.split_after_handover", "errors.extract.split_after_handover");

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

    public static readonly Error NotFound =
        Error.NotFound("extract.not_found", "errors.extract.not_found");

    /// <summary>
    /// Another extract of the project is still in flight, so its executed quantities are not yet certified and a
    /// new one would bill the same work twice. Assumed, not ruled: see D-194.
    /// </summary>
    public static readonly Error AnotherExtractOpen =
        Error.Conflict("extract.another_extract_open", "errors.extract.another_extract_open");

    /// <summary>The project lacks an account the extract's postings need (the safe, or a ledger account).</summary>
    public static readonly Error AccountsMissing =
        Error.Conflict("extract.accounts_missing", "errors.extract.accounts_missing");

    /// <summary>Two extracts were prepared at the same moment and took the same number. Retry.</summary>
    public static readonly Error NumberTaken =
        Error.Conflict("extract.number_taken", "errors.extract.number_taken");

    /// <summary>Evidence can be revised only on a Draft extract that a rejection returned.</summary>
    public static readonly Error RevisionNotAllowed =
        Error.Conflict("extract.revision_not_allowed", "errors.extract.revision_not_allowed");

    /// <summary>A rejection route was called for an extract that does not wait at that route's gate.</summary>
    public static readonly Error NotWaitingAtThisGate =
        Error.Conflict("extract.not_waiting_at_this_gate", "errors.extract.not_waiting_at_this_gate");

    /// <summary>The extract named as revised is not a cancelled extract of this project, or already has its revision.</summary>
    public static readonly Error RevisionTargetInvalid =
        Error.Conflict("extract.revision_target_invalid", "errors.extract.revision_target_invalid");

    /// <summary>D-242 item 2: a rejection names what was wrong, quantities or material values.</summary>
    public static readonly Error RejectionKindRequired =
        Error.Validation("extract.rejection_kind_required", "errors.extract.rejection_kind_required");

    /// <summary>Rejected for wrong quantities: the site engineer revises the evidence before the QC report goes in again.</summary>
    public static readonly Error AwaitingEvidenceRevision =
        Error.Conflict("extract.awaiting_evidence_revision", "errors.extract.awaiting_evidence_revision");

    /// <summary>
    /// A cheque in hand or a credit note stands on the disputed original; what a revision does to either is not ruled.
    /// Payments already received move to the revision (D-242 item 3) and do not block.
    /// </summary>
    public static readonly Error RevisionMoneyStanding =
        Error.Conflict("extract.revision_money_standing", "errors.extract.revision_money_standing");

    /// <summary>A later extract was prepared after the disputed one, so its previously certified work would no longer hold.</summary>
    public static readonly Error RevisionNotLatest =
        Error.Conflict("extract.revision_not_latest", "errors.extract.revision_not_latest");

    /// <summary>The disputed original billed a change order, which may be billed once only.</summary>
    public static readonly Error RevisionChangeOrdersBilled =
        Error.Conflict("extract.revision_change_orders_billed", "errors.extract.revision_change_orders_billed");

    /// <summary>A credit note is open against the extract, so a collection of its full net would over-collect (Q106, Q107).</summary>
    public static readonly Error CreditNoteOpen =
        Error.Conflict("extract.credit_note_open", "errors.extract.credit_note_open");

    public static readonly Error DisputeResolutionRequired =
        Error.Validation("extract.dispute_resolution_required", "errors.extract.dispute_resolution_required");

    /// <summary>D-235 item 4: the supervising engineer enters both material values with the QC report.</summary>
    public static readonly Error MaterialValuesRequired =
        Error.Validation("extract.material_values_required", "errors.extract.material_values_required");

    /// <summary>AC-500-E: an NCR closes once.</summary>
    public static readonly Error NcrAlreadyClosed =
        Error.Conflict("extract.ncr_already_closed", "errors.extract.ncr_already_closed");

    /// <summary>AC-500-E: closing an NCR stores why it closed.</summary>
    public static readonly Error NcrCloseNoteRequired =
        Error.Validation("extract.ncr_close_note_required", "errors.extract.ncr_close_note_required");

    public static readonly Error NcrNotFound =
        Error.NotFound("extract.ncr_not_found", "errors.extract.ncr_not_found");
}
