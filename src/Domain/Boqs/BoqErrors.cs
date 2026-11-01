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
}
