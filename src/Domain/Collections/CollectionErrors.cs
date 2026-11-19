using Kaff.Domain.Common;

namespace Kaff.Domain.Collections;

/// <summary>Error catalogue for collections and cheques (spec.md section 6.5).</summary>
public static class CollectionErrors
{
    public static readonly Error MethodRequired =
        Error.Validation("collection.method_required", "errors.collection.method_required");

    public static readonly Error DateRequired =
        Error.Validation("collection.date_required", "errors.collection.date_required");

    public static readonly Error ReferenceRequired =
        Error.Validation("collection.reference_required", "errors.collection.reference_required");

    public static readonly Error ReferenceTooLong =
        Error.Validation("collection.reference_too_long", "errors.collection.reference_too_long");

    public static readonly Error ChequeIllegalTransition =
        Error.Conflict("cheque.illegal_transition", "errors.cheque.illegal_transition");

    public static readonly Error ChequeNotFound =
        Error.NotFound("cheque.not_found", "errors.cheque.not_found");

    /// <summary>D-235 item 2: a collection records the amount received. It is never inferred from the extract.</summary>
    public static readonly Error AmountRequired =
        Error.Validation("collection.amount_required", "errors.collection.amount_required");

    /// <summary>Neither amount may be negative, and together they must be more than zero.</summary>
    public static readonly Error AmountInvalid =
        Error.Validation("collection.amount_invalid", "errors.collection.amount_invalid");

    /// <summary>
    /// D-235 item 2: received plus withheld may not pass what the extract still expects, counting cheques in hand.
    /// </summary>
    public static readonly Error ExceedsExpected =
        Error.Conflict("collection.exceeds_expected", "errors.collection.exceeds_expected");

    /// <summary>
    /// Nothing remains on the extract: what was collected and the cheques in hand cover all that is expected. Returned
    /// before any amount is read (copy review 2026-11-18, item 1).
    /// </summary>
    public static readonly Error NothingRemaining =
        Error.Conflict("collection.nothing_remaining", "errors.collection.nothing_remaining");

    /// <summary>spec.md section 6.7: a contract with no withholding category withholds nothing (AC-317-B, AC-317-F).</summary>
    public static readonly Error WithholdingNotApplicable =
        Error.Validation("collection.withholding_not_applicable", "errors.collection.withholding_not_applicable");
}
