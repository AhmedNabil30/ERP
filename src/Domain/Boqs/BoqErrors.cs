using Kaff.Domain.Common;

namespace Kaff.Domain.Boqs;

/// <summary>Error catalogue for the BOQ (spec.md §4).</summary>
public static class BoqErrors
{
    public static readonly Error NotFound =
        Error.NotFound("boq.not_found", "errors.boq.not_found");

    /// <summary>Signing needs a BOQ to take the contract value from.</summary>
    public static readonly Error Required =
        Error.Conflict("boq.required", "errors.boq.required");

    public static readonly Error Empty =
        Error.Conflict("boq.empty", "errors.boq.empty");

    /// <summary>spec.md §4.4: a signed BOQ is a frozen copy. No line may be added, changed or removed.</summary>
    public static readonly Error Frozen =
        Error.Conflict("boq.frozen", "errors.boq.frozen");

    public static readonly Error AlreadyFrozen =
        Error.Conflict("boq.already_frozen", "errors.boq.already_frozen");

    public static readonly Error LineNotFound =
        Error.NotFound("boq.line_not_found", "errors.boq.line_not_found");

    /// <summary>The BOQ total is priced on a quantity state that this line does not have yet.</summary>
    public static readonly Error LineQuantityMissing =
        Error.Conflict("boq.line_quantity_missing", "errors.boq.line_quantity_missing");

    /// <summary>spec.md §5.3: a Design project has no BOQ.</summary>
    public static readonly Error NotForThisContractType =
        Error.Conflict("boq.not_for_this_contract_type", "errors.boq.not_for_this_contract_type");

    /// <summary>A new line named neither a catalogue item nor the fields of a custom item.</summary>
    public static readonly Error LineShapeInvalid =
        Error.Validation("boq.line_shape_invalid", "errors.boq.line_shape_invalid");

    public static readonly Error QuantityMustNotBeNegative =
        Error.Validation("boq.quantity_must_not_be_negative", "errors.boq.quantity_must_not_be_negative");

    public static readonly Error CatalogueItemArchived =
        Error.Conflict("boq.catalogue_item_archived", "errors.boq.catalogue_item_archived");

    public static readonly Error BaseNotFound =
        Error.NotFound("boq.base_not_found", "errors.boq.base_not_found");

    public static readonly Error BaseNameRequired =
        Error.Validation("boq.base_name_required", "errors.boq.base_name_required");

    /// <summary>An archived base is hidden and read-only.</summary>
    public static readonly Error BaseArchived =
        Error.Conflict("boq.base_archived", "errors.boq.base_archived");

    /// <summary>D-217 item 20: a base or an earlier project fills an empty BOQ only.</summary>
    public static readonly Error NotEmpty =
        Error.Conflict("boq.not_empty", "errors.boq.not_empty");

    /// <summary>A copy named neither a base nor a source project, or both.</summary>
    public static readonly Error CopySourceInvalid =
        Error.Validation("boq.copy_source_invalid", "errors.boq.copy_source_invalid");

    /// <summary>The source project has no BOQ, or the base has no line, so there is nothing to copy.</summary>
    public static readonly Error CopySourceEmpty =
        Error.Conflict("boq.copy_source_empty", "errors.boq.copy_source_empty");

    public static readonly Error BatchEmpty =
        Error.Validation("boq.batch_empty", "errors.boq.batch_empty");
}
