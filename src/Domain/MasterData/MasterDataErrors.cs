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

    /// <summary>spec.md §6.7 and KAFF-119 rule 8 — a client is either Individual or Corporate.</summary>
    /// <remarks>
    /// A shape rule, not a business one: an absent <c>kind</c> binds to the enum's zero, which is not
    /// a member, and would be stored as the text <c>"0"</c> by the enum-as-string convention. The
    /// entity cannot refuse it, because by the time it holds a <c>ClientKind</c> the value is already
    /// whatever the binder produced.
    /// </remarks>
    public static readonly Error ClientKindRequired =
        Error.Validation("master.client_kind_required", "errors.master.client_kind_required");

    /// <summary>
    /// The phone is already on file and the request did not say the operator had seen the warning.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This does not block the save — it asks.</b> spec.md §2's amendment: <i>"A repeated number
    /// shows the operator which client already holds it and asks whether to proceed. It does not
    /// block the save."</i> The same request with <c>acknowledgedDuplicatePhone</c> succeeds, by the
    /// same actor, with the same data — which is what makes this a question rather than a refusal.
    /// </para>
    /// <para>
    /// It exists because without it a caller that never ran the check creates a duplicate and the
    /// trail is silent about it, permanently, in an append-only table. The <b>warning</b> itself is
    /// never this error: it is a 200 body from <c>POST /api/clients/phone-check</c> naming the
    /// matched clients, because a ProblemDetails cannot carry them — the SPA keeps only status, code
    /// and messageKey. See decisions.md D-107 §2.
    /// </para>
    /// </remarks>
    public static readonly Error DuplicatePhoneNotAcknowledged =
        Error.Conflict("master.duplicate_phone_not_acknowledged", "errors.master.duplicate_phone_not_acknowledged");

    /// <summary>The route named a client id that no client carries. KAFF-121.</summary>
    /// <remarks>
    /// The same shape as <c>IdentityErrors.UserNotFound</c>: an endpoint that addresses a row by an
    /// id in its route has to say something translatable when the id names nobody, rather than a bare
    /// 404 the SPA can only render as "something went wrong".
    /// </remarks>
    public static readonly Error ClientNotFound =
        Error.NotFound("master.client_not_found", "errors.master.client_not_found");
}
