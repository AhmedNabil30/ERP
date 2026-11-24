using Kaff.Domain.Common;

namespace Kaff.Domain.Closure;

/// <summary>Error catalogue for practical completion, the snag list, handover, warranty, the hold release and closure.</summary>
public static class ClosureErrors
{
    /// <summary>A Design project has no snag list, no handover, no warranty and no hold (spec.md section 11, D-015).</summary>
    public static readonly Error NotApplicableToDesign =
        Error.Conflict("closure.not_applicable_to_design", "errors.closure.not_applicable_to_design");

    /// <summary>The hold exists on a Lump Sum contract only (spec.md sections 5.2, 5.3).</summary>
    public static readonly Error HoldNotApplicable =
        Error.Conflict("closure.hold_not_applicable", "errors.closure.hold_not_applicable");

    public static readonly Error MajorSnagsOpen =
        Error.Conflict("closure.major_snags_open", "errors.closure.major_snags_open");

    /// <summary>An NCR on the project is still open, so part of an extract is still held (D-254 item 2).</summary>
    public static readonly Error NcrOpen =
        Error.Conflict("closure.ncr_open", "errors.closure.ncr_open");

    /// <summary>A released remainder has not yet been billed on an extract (D-254 item 2, D-253).</summary>
    public static readonly Error ReleasedRemainderUnbilled =
        Error.Conflict("closure.released_remainder_unbilled", "errors.closure.released_remainder_unbilled");

    /// <summary>Terminated is unruled: spec.md names the state and a settlement and says what settles nowhere.</summary>
    public static readonly Error TerminationSettlementUnruled =
        Error.Conflict("closure.termination_settlement_unruled", "errors.closure.termination_settlement_unruled");

    public static readonly Error NotSettled =
        Error.Conflict("closure.not_settled", "errors.closure.not_settled");

    public static readonly Error HoldAlreadyReleased =
        Error.Conflict("closure.hold_already_released", "errors.closure.hold_already_released");

    /// <summary>AC-802-L: a hold release request is already waiting for the Owner.</summary>
    public static readonly Error HoldReleaseAlreadyRequested =
        Error.Conflict("closure.hold_release_already_requested", "errors.closure.hold_release_already_requested");

    /// <summary>AC-802-L: no hold release request is waiting to be approved or rejected.</summary>
    public static readonly Error HoldReleaseNotRequested =
        Error.Conflict("closure.hold_release_not_requested", "errors.closure.hold_release_not_requested");

    /// <summary>AC-806-E: a retention release request for this subcontractor is already waiting for the Owner.</summary>
    public static readonly Error RetentionReleaseAlreadyRequested =
        Error.Conflict("closure.retention_release_already_requested", "errors.closure.retention_release_already_requested");

    /// <summary>AC-806-E: no retention release request is waiting to be approved or rejected.</summary>
    public static readonly Error RetentionReleaseNotRequested =
        Error.Conflict("closure.retention_release_not_requested", "errors.closure.retention_release_not_requested");

    /// <summary>AC-806-D: the posting named is not a standing retention release of this subcontractor on this project.</summary>
    public static readonly Error RetentionReleasePostingNotFound =
        Error.Conflict("closure.retention_release_posting_not_found", "errors.closure.retention_release_posting_not_found");

    /// <summary>AC-806-D: that retention release is already reversed.</summary>
    public static readonly Error RetentionReleaseAlreadyReversed =
        Error.Conflict("closure.retention_release_already_reversed", "errors.closure.retention_release_already_reversed");

    /// <summary>AC-806-D: a reversal request for this release is already waiting for the Owner.</summary>
    public static readonly Error RetentionReversalAlreadyRequested =
        Error.Conflict("closure.retention_reversal_already_requested", "errors.closure.retention_reversal_already_requested");

    /// <summary>AC-806-D: no retention reversal request is waiting to be approved or returned.</summary>
    public static readonly Error RetentionReversalNotRequested =
        Error.Conflict("closure.retention_reversal_not_requested", "errors.closure.retention_reversal_not_requested");

    public static readonly Error NothingToRelease =
        Error.Conflict("closure.nothing_to_release", "errors.closure.nothing_to_release");

    public static readonly Error AccountsMissing =
        Error.Conflict("closure.accounts_missing", "errors.closure.accounts_missing");

    /// <summary>A snag is raised on the list kept between practical completion and handover (spec.md section 11).</summary>
    public static readonly Error SnagListNotOpen =
        Error.Conflict("closure.snag_list_not_open", "errors.closure.snag_list_not_open");

    public static readonly Error SnagNotFound =
        Error.NotFound("closure.snag_not_found", "errors.closure.snag_not_found");

    public static readonly Error SnagDescriptionRequired =
        Error.Validation("closure.snag_description_required", "errors.closure.snag_description_required");

    public static readonly Error SnagLocationRequired =
        Error.Validation("closure.snag_location_required", "errors.closure.snag_location_required");

    public static readonly Error SnagOwnerRequired =
        Error.Validation("closure.snag_owner_required", "errors.closure.snag_owner_required");

    public static readonly Error SnagPriorityRequired =
        Error.Validation("closure.snag_priority_required", "errors.closure.snag_priority_required");

    public static readonly Error SnagIllegalTransition =
        Error.Conflict("closure.snag_illegal_transition", "errors.closure.snag_illegal_transition");

    public static readonly Error SnagNotFixed =
        Error.Conflict("closure.snag_not_fixed", "errors.closure.snag_not_fixed");

    public static readonly Error SnagAlreadyResolved =
        Error.Conflict("closure.snag_already_resolved", "errors.closure.snag_already_resolved");

    public static readonly Error DebitNeedsSubcontractorOwner =
        Error.Conflict("closure.debit_needs_subcontractor_owner", "errors.closure.debit_needs_subcontractor_owner");

    public static readonly Error ResolutionRequired =
        Error.Validation("closure.resolution_required", "errors.closure.resolution_required");

    public static readonly Error ReasonRequired =
        Error.Validation("closure.reason_required", "errors.closure.reason_required");

    public static readonly Error SubcontractorNotFound =
        Error.NotFound("closure.subcontractor_not_found", "errors.closure.subcontractor_not_found");

    public static readonly Error CallbackNotFound =
        Error.NotFound("closure.callback_not_found", "errors.closure.callback_not_found");

    public static readonly Error CallbackNoWarranty =
        Error.Conflict("closure.callback_no_warranty", "errors.closure.callback_no_warranty");

    public static readonly Error CallbackAfterWarranty =
        Error.Conflict("closure.callback_after_warranty", "errors.closure.callback_after_warranty");

    public static readonly Error CallbackDescriptionRequired =
        Error.Validation("closure.callback_description_required", "errors.closure.callback_description_required");

    public static readonly Error CallbackAlreadyResolved =
        Error.Conflict("closure.callback_already_resolved", "errors.closure.callback_already_resolved");

    /// <summary>Subcontractor retention releases when the warranty ends, not before (spec.md section 5.1).</summary>
    public static readonly Error WarrantyNotEnded =
        Error.Conflict("closure.warranty_not_ended", "errors.closure.warranty_not_ended");

    /// <summary>D-244 item 2: a released hold is collected only once the hold has been released.</summary>
    public static readonly Error HoldNotReleased =
        Error.Conflict("closure.hold_not_released", "errors.closure.hold_not_released");

    /// <summary>D-244 item 2: what was collected and the cheques in hand already cover the released hold.</summary>
    public static readonly Error ReleasedHoldNothingRemaining =
        Error.Conflict("closure.released_hold_nothing_remaining", "errors.closure.released_hold_nothing_remaining");

    /// <summary>D-244 item 2: received plus withheld may not pass what remains of the released hold.</summary>
    public static readonly Error ReleasedHoldExceeded =
        Error.Conflict("closure.released_hold_exceeded", "errors.closure.released_hold_exceeded");

    /// <summary>AC-317-E: the ruling says the hold release carries no withholding, so a withheld amount on it is refused.</summary>
    public static readonly Error ReleasedHoldCarriesNoWithholding =
        Error.Validation("closure.released_hold_carries_no_withholding", "errors.closure.released_hold_carries_no_withholding");
}
