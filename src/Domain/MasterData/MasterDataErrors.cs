using Kaff.Domain.Common;

namespace Kaff.Domain.MasterData;

/// <summary>Error catalogue for the master records of spec.md §2.</summary>
public static class MasterDataErrors
{
    public static readonly Error CodeRequired =
        Error.Validation("master.code_required", "errors.master.code_required");

    public static readonly Error NameRequired =
        Error.Validation("master.name_required", "errors.master.name_required");

    /// <summary>KAFF-204 rule 2 — every باب carries its own required markup; there is no null and no inheritance.</summary>
    public static readonly Error DefaultMarkupRequired =
        Error.Validation("master.default_markup_required", "errors.master.default_markup_required");

    public static readonly Error UnitRequired =
        Error.Validation("master.unit_required", "errors.master.unit_required");

    public static readonly Error DescriptionRequired =
        Error.Validation("master.description_required", "errors.master.description_required");

    public static readonly Error CostPriceMustNotBeNegative =
        Error.Validation("master.cost_price_negative", "errors.master.cost_price_negative");

    public static readonly Error SellRateMustNotBeNegative =
        Error.Validation("master.sell_rate_negative", "errors.master.sell_rate_negative");

    /// <summary>
    /// V-36-H: a blank or omitted price was silently stored as <c>0.0000</c> — a money value invented
    /// by the system. An explicit <c>0</c> stays legal (AC-202-D refuses only negatives); this refuses
    /// only the absence of a value.
    /// </summary>
    public static readonly Error CostPriceRequired =
        Error.Validation("master.cost_price_required", "errors.master.cost_price_required");

    /// <summary>V-36-H, the sell-rate half of <see cref="CostPriceRequired"/>.</summary>
    public static readonly Error SellRateRequired =
        Error.Validation("master.sell_rate_required", "errors.master.sell_rate_required");

    public static readonly Error AlreadyArchived =
        Error.Conflict("master.already_archived", "errors.master.already_archived");

    public static readonly Error NotArchived =
        Error.Conflict("master.not_archived", "errors.master.not_archived");

    /// <summary>
    /// Renamed from <c>BabCannotBeItsOwnParent</c> — decisions.md D-128. "Cannot be its own parent" is
    /// false of the depth-two-or-more refusal <c>Bab.SetParent</c> also returns this for (A being made
    /// its own grandparent is not a parent relationship at all), so SM-33 requires the rename rather
    /// than a second key beside the false one. One guard, one message, in every depth it refuses.
    /// </summary>
    public static readonly Error BabCannotBeItsOwnAncestor =
        Error.Validation(
            "master.bab_cannot_be_its_own_ancestor", "errors.master.bab_cannot_be_its_own_ancestor");

    /// <summary>spec.md §4.5 — a code that identifies two أبواب identifies neither. KAFF-204 rule, `ux_babs_code`.</summary>
    public static readonly Error BabCodeTaken =
        Error.Conflict("master.bab_code_taken", "errors.master.bab_code_taken");

    /// <summary>KAFF-213, D-130 §5 — a باب holding active items cannot be archived; the refusal names the count.</summary>
    public static readonly Error BabHasActiveItems =
        Error.Conflict("master.bab_has_active_items", "errors.master.bab_has_active_items");

    /// <summary>spec.md §10: "Nobody appears in both" — day labour and salaried staff are distinct populations.</summary>
    public static readonly Error EmployeeKindIsImmutable =
        Error.Conflict("master.employee_kind_immutable", "errors.master.employee_kind_immutable");

    public static readonly Error DayLabourRequiresTrade =
        Error.Validation("master.day_labour_requires_trade", "errors.master.day_labour_requires_trade");

    /// <summary>The route named an employee id that no employee carries. KAFF-207.</summary>
    public static readonly Error EmployeeNotFound =
        Error.NotFound("master.employee_not_found", "errors.master.employee_not_found");

    /// <summary>
    /// A salaried record's phone matches another <b>active</b> salaried record. Enforced by
    /// <c>ux_employees_salaried_phone</c>, a partial unique index scoped to
    /// <c>kind = 'Salaried' AND is_active</c>, not by a read-then-write (decisions.md D-144 §1, D-146
    /// point 2-3, narrowed to active-only by D-153 §3, Q83). An archived salaried record's phone is
    /// free — a match against one is <see cref="DuplicatePhoneNotAcknowledged"/>'s warn-and-acknowledge,
    /// the same as day labour, subcontractors and suppliers (D-139 §1, D-141). The salaried-only scope
    /// narrowed from "any employee" when D-141 briefly withdrew this error (superseded); it stayed live
    /// throughout.
    /// </summary>
    public static readonly Error EmployeePhoneTaken =
        Error.Conflict("master.employee_phone_taken", "errors.master.employee_phone_taken");

    /// <summary>The employee list's <c>status</c> filter named something that is not a filter. KAFF-207.</summary>
    /// <remarks>Same shape and reasoning as <see cref="CatalogueItemListFilterUnknown"/> — D-111 §3.</remarks>
    public static readonly Error EmployeeListFilterUnknown =
        Error.Validation("master.employee_list_filter_unknown", "errors.master.employee_list_filter_unknown");

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

    /// <summary>The <c>status</c> filter on the client list named something that is not a filter. KAFF-124.</summary>
    /// <remarks>
    /// <b>Refused rather than defaulted.</b> Treating <c>?status=archvied</c> as "active" answers a
    /// question nobody asked and is indistinguishable from an empty archive — the operator concludes
    /// there is nothing there. An absent filter is a default; a wrong one is a mistake, and the two
    /// must not produce the same list.
    /// </remarks>
    public static readonly Error ClientListFilterUnknown =
        Error.Validation("master.client_list_filter_unknown", "errors.master.client_list_filter_unknown");

    /// <summary>KAFF-202 rule 9: an item's باب is required and must exist.</summary>
    public static readonly Error BabNotFound =
        Error.NotFound("master.bab_not_found", "errors.master.bab_not_found");

    /// <summary>The route named a catalogue item id that no item carries. KAFF-202.</summary>
    public static readonly Error CatalogueItemNotFound =
        Error.NotFound("master.catalogue_item_not_found", "errors.master.catalogue_item_not_found");

    /// <summary>
    /// spec.md §4.5 — a code that identifies two items identifies neither. Enforced by
    /// <c>ux_catalogue_items_code</c>, not by a read-then-write in the handler (KAFF-202 rule 3).
    /// </summary>
    public static readonly Error CatalogueItemCodeTaken =
        Error.Conflict("master.catalogue_item_code_taken", "errors.master.catalogue_item_code_taken");

    /// <summary>The catalogue list's <c>status</c> filter named something that is not a filter. KAFF-206 rule 7.</summary>
    /// <remarks>Same shape and same reasoning as <see cref="ClientListFilterUnknown"/> — D-111 §3.</remarks>
    public static readonly Error CatalogueItemListFilterUnknown =
        Error.Validation(
            "master.catalogue_item_list_filter_unknown", "errors.master.catalogue_item_list_filter_unknown");

    /// <summary>The باب list's <c>status</c> filter named something that is not a filter. KAFF-213 rule 9.</summary>
    /// <remarks>Same shape and reasoning as <see cref="CatalogueItemListFilterUnknown"/> — D-111 §3.</remarks>
    public static readonly Error BabListFilterUnknown =
        Error.Validation("master.bab_list_filter_unknown", "errors.master.bab_list_filter_unknown");

    /// <summary>
    /// KAFF-200 — a file-level refusal: not a zip, missing a required OOXML part, an unreadable
    /// header row, no data rows, or above one of decisions.md D-136's technical ceilings. One shared
    /// key, per the story's own i18n list — the row-level report carries the granular reasons instead.
    /// </summary>
    public static readonly Error CatalogueImportFailed =
        Error.Validation("master.catalogue_import_failed", "errors.master.catalogue_import_failed");

    /// <summary>
    /// KAFF-200, D-136's row table — a cost or sell cell that is text failing the wire grammar, a
    /// boolean, or an error cell (<c>#VALUE!</c>). Distinct from <see cref="CostPriceRequired"/> /
    /// <see cref="SellRateRequired"/>, which name an <b>absent</b> value rather than a malformed one.
    /// </summary>
    public static readonly Error CatalogueImportBadNumber =
        Error.Validation("master.catalogue_import_bad_number", "errors.master.catalogue_import_bad_number");

    /// <summary>
    /// KAFF-200, D-136's row table — a code repeated within the same file. The first occurrence
    /// imports; every later one is refused with this, distinct from <see cref="CatalogueItemCodeTaken"/>
    /// which names a code already in the catalogue before the file was opened.
    /// </summary>
    public static readonly Error CatalogueItemCodeRepeatedInFile =
        Error.Validation(
            "master.catalogue_item_code_repeated_in_file", "errors.master.catalogue_item_code_repeated_in_file");

    // ---- KAFF-210: engagement history ----

    /// <summary>Rule 4 — a day rate is what was agreed, never invented. An explicit non-positive value is refused.</summary>
    public static readonly Error EngagementDayRateMustBePositive =
        Error.Validation("master.engagement_day_rate_must_be_positive", "errors.master.engagement_day_rate_must_be_positive");

    /// <summary>D-139 §3 — an engagement closes only once, by an explicit manual close.</summary>
    public static readonly Error EngagementAlreadyClosed =
        Error.Conflict("master.engagement_already_closed", "errors.master.engagement_already_closed");

    /// <summary>AC-210-F, D-139 §3 — a rating is refused outside 1–5.</summary>
    public static readonly Error EngagementRatingOutOfRange =
        Error.Validation("master.engagement_rating_out_of_range", "errors.master.engagement_rating_out_of_range");

    /// <summary>The route named an engagement id that no engagement carries. KAFF-210.</summary>
    public static readonly Error EngagementNotFound =
        Error.NotFound("master.engagement_not_found", "errors.master.engagement_not_found");

    /// <summary>
    /// decisions.md D-152 §3, §4 / D-153 §1 point 4/7 — only the Owner or the engineer who opened the
    /// engagement may set its day rate or record its rating. Anyone else holding
    /// <c>DayLabourRateManage</c> or <c>DayLabourSiteManage</c> on this project is refused this one
    /// engagement, not the permission itself.
    /// </summary>
    public static readonly Error EngagementNotResponsibleEngineer = Error.Forbidden(
        "master.engagement_not_responsible_engineer", "errors.master.engagement_not_responsible_engineer");

    /// <summary>
    /// <c>SetEngagementDayRate.Request.DayRate</c> is <c>Money?</c> so an omitted member is refused
    /// rather than silently read as zero — the same shape D-151 §6a gives
    /// <c>EditSubcontractor.Request.RetentionRate</c>.
    /// </summary>
    public static readonly Error EngagementDayRateRequired = Error.Validation(
        "master.engagement_day_rate_required", "errors.master.engagement_day_rate_required");

    // ---- KAFF-211: subcontractor master ----

    /// <summary>The route named a subcontractor id that no subcontractor carries. KAFF-211.</summary>
    public static readonly Error SubcontractorNotFound =
        Error.NotFound("master.subcontractor_not_found", "errors.master.subcontractor_not_found");

    /// <summary>
    /// D-151 §6a — <c>EditSubcontractor.Request.RetentionRate</c> is <c>Percentage?</c>; an omitted
    /// member must not deserialise to zero and silently zero a firm's retention. A negative rate is
    /// refused earlier, by the type itself, and surfaces as <see cref="ApiErrors.MalformedBody"/>.
    /// </summary>
    public static readonly Error RetentionRateRequired =
        Error.Validation("master.retention_rate_required", "errors.master.retention_rate_required");

    /// <summary>The subcontractor list's <c>status</c> filter named something that is not a filter. KAFF-211.</summary>
    public static readonly Error SubcontractorListFilterUnknown =
        Error.Validation("master.subcontractor_list_filter_unknown", "errors.master.subcontractor_list_filter_unknown");

    /// <summary>
    /// Rule 6a, D-140's SM-30 test 14: the route's project does not match the engagement's own
    /// project. Forbidden rather than NotFound — an engineer assigned to the route's project is
    /// authorized to act on ITS engagements, and this refusal is about which engagement, not whether
    /// the id exists at all.
    /// </summary>
    public static readonly Error EngagementProjectMismatch =
        Error.Forbidden("master.engagement_project_mismatch", "errors.master.engagement_project_mismatch");

    // ---- KAFF-212: supplier master ----

    /// <summary>The route named a supplier id that no supplier carries. KAFF-212.</summary>
    public static readonly Error SupplierNotFound =
        Error.NotFound("master.supplier_not_found", "errors.master.supplier_not_found");

    /// <summary>The supplier list's <c>status</c> filter named something that is not a filter. KAFF-212.</summary>
    public static readonly Error SupplierListFilterUnknown =
        Error.Validation("master.supplier_list_filter_unknown", "errors.master.supplier_list_filter_unknown");
}
