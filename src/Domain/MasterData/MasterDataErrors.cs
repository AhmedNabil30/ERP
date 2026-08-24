using Kaff.Domain.Common;

namespace Kaff.Domain.MasterData;

/// <summary>Error catalogue for the master records of spec.md §2.</summary>
public static class MasterDataErrors
{
    public static readonly Error CodeRequired =
        Error.Validation("master.code_required", "errors.master.code_required");

    public static readonly Error NameRequired =
        Error.Validation("master.name_required", "errors.master.name_required");

    public static readonly Error UnitRequired =
        Error.Validation("master.unit_required", "errors.master.unit_required");

    public static readonly Error DescriptionRequired =
        Error.Validation("master.description_required", "errors.master.description_required");

    public static readonly Error CostPriceMustNotBeNegative =
        Error.Validation("master.cost_price_negative", "errors.master.cost_price_negative");

    public static readonly Error SellRateMustNotBeNegative =
        Error.Validation("master.sell_rate_negative", "errors.master.sell_rate_negative");

    public static readonly Error AlreadyArchived =
        Error.Conflict("master.already_archived", "errors.master.already_archived");

    public static readonly Error NotArchived =
        Error.Conflict("master.not_archived", "errors.master.not_archived");

    public static readonly Error BabCannotBeItsOwnParent =
        Error.Validation("master.bab_cannot_be_its_own_parent", "errors.master.bab_cannot_be_its_own_parent");

    /// <summary>spec.md §10: "Nobody appears in both" — day labour and salaried staff are distinct populations.</summary>
    public static readonly Error EmployeeKindIsImmutable =
        Error.Conflict("master.employee_kind_immutable", "errors.master.employee_kind_immutable");

    public static readonly Error DayLabourRequiresTrade =
        Error.Validation("master.day_labour_requires_trade", "errors.master.day_labour_requires_trade");

    public static readonly Error ClosedLostRequiresReason =
        Error.Validation("master.closed_lost_requires_reason", "errors.master.closed_lost_requires_reason");

    /// <summary>
    /// spec.md §6.7: "Individual clients do not withhold." Raised both by a tax registration number
    /// on an individual and by a withholding rate on a project whose client is one. See D-040, D-049.
    /// </summary>
    public static readonly Error IndividualDoesNotWithhold =
        Error.Validation("master.individual_does_not_withhold", "errors.master.individual_does_not_withhold");
}
