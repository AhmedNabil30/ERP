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
}
