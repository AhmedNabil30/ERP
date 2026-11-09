using Kaff.Domain.Common;

namespace Kaff.Domain.Adjustments;

/// <summary>Error catalogue for the one Adjustment object (spec.md section 6.9).</summary>
public static class AdjustmentErrors
{
    public static readonly Error NotFound =
        Error.NotFound("adjustment.not_found", "errors.adjustment.not_found");

    public static readonly Error ReasonRequired =
        Error.Validation("adjustment.reason_required", "errors.adjustment.reason_required");

    public static readonly Error ReasonTooLong =
        Error.Validation("adjustment.reason_too_long", "errors.adjustment.reason_too_long");

    public static readonly Error TypeRequired =
        Error.Validation("adjustment.type_required", "errors.adjustment.type_required");

    public static readonly Error SourceDocumentRequired =
        Error.Validation("adjustment.source_document_required", "errors.adjustment.source_document_required");

    public static readonly Error TargetAccountRequired =
        Error.Validation("adjustment.target_account_required", "errors.adjustment.target_account_required");

    /// <summary>Zero and negative amounts are refused: an adjustment is a positive amount in a named direction (Q107).</summary>
    public static readonly Error AmountMustBePositive =
        Error.Validation("adjustment.amount_must_be_positive", "errors.adjustment.amount_must_be_positive");

    /// <summary>The hold only grows (spec.md section 5.1): no adjustment may name it.</summary>
    public static readonly Error HoldUntouchable =
        Error.Conflict("adjustment.hold_untouchable", "errors.adjustment.hold_untouchable");

    /// <summary>A credit note lands on the project's contract revenue account and on no other (Q107, assumed).</summary>
    public static readonly Error TargetAccountNotAllowed =
        Error.Conflict("adjustment.target_account_not_allowed", "errors.adjustment.target_account_not_allowed");

    public static readonly Error TypeNotBuilt =
        Error.Conflict("adjustment.type_not_built", "errors.adjustment.type_not_built");

    /// <summary>spec.md section 13 and 6.9 name a termination settlement and say what settles nowhere (Q108).</summary>
    public static readonly Error TerminationSettlementUnruled =
        Error.Conflict("adjustment.termination_settlement_unruled", "errors.adjustment.termination_settlement_unruled");

    public static readonly Error SourceNotCreditable =
        Error.Conflict("adjustment.source_not_creditable", "errors.adjustment.source_not_creditable");

    public static readonly Error ExceedsExtractNet =
        Error.Conflict("adjustment.exceeds_extract_net", "errors.adjustment.exceeds_extract_net");

    public static readonly Error ProjectStateNotAllowed =
        Error.Conflict("adjustment.project_state_not_allowed", "errors.adjustment.project_state_not_allowed");

    public static readonly Error IllegalTransition =
        Error.Conflict("adjustment.illegal_transition", "errors.adjustment.illegal_transition");

    public static readonly Error AccountsMissing =
        Error.Conflict("adjustment.accounts_missing", "errors.adjustment.accounts_missing");

    public static readonly Error ReversalPostingsRequired =
        Error.Conflict("adjustment.reversal_postings_required", "errors.adjustment.reversal_postings_required");

    public static readonly Error NumberTaken =
        Error.Conflict("adjustment.number_taken", "errors.adjustment.number_taken");

    /// <summary>KAFF-415, Q-207-6: the design credit is 30% of the design contract value, and there is none.</summary>
    public static readonly Error DesignValueMissing =
        Error.Conflict("adjustment.design_value_missing", "errors.adjustment.design_value_missing");

    /// <summary>KAFF-415: a design credit is raised only for an execution project linked to that design project.</summary>
    public static readonly Error DesignCreditNotLinked =
        Error.Conflict("adjustment.design_credit_not_linked", "errors.adjustment.design_credit_not_linked");

    /// <summary>KAFF-415: one design credit per execution project.</summary>
    public static readonly Error DesignCreditAlreadyRaised =
        Error.Conflict("adjustment.design_credit_already_raised", "errors.adjustment.design_credit_already_raised");
}
