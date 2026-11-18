using Kaff.Domain.Common;

namespace Kaff.Domain.Treasury;

/// <summary>
/// Error catalogue for the ledger.
/// </summary>
/// <remarks>
/// Every rule here is also enforced by a database trigger. That is deliberate: the domain check
/// gives the user a translated, actionable message, and the database check is what makes the rule
/// true regardless of which code path — or which future session — reaches the table. If the two ever
/// disagree, the database wins and the domain has a bug.
/// </remarks>
public static class TreasuryErrors
{
    // ---- Account ----

    public static readonly Error AccountCodeRequired =
        Error.Validation("treasury.account_code_required", "errors.treasury.account_code_required");

    public static readonly Error AccountNameRequired =
        Error.Validation("treasury.account_name_required", "errors.treasury.account_name_required");

    public static readonly Error AccountRequiresProject =
        Error.Validation("treasury.account_requires_project", "errors.treasury.account_requires_project");

    public static readonly Error AccountMustNotCarryProject =
        Error.Validation("treasury.account_must_not_carry_project", "errors.treasury.account_must_not_carry_project");

    public static readonly Error AccountRequiresParty =
        Error.Validation("treasury.account_requires_party", "errors.treasury.account_requires_party");

    public static readonly Error AccountPartyTypeMismatch =
        Error.Validation("treasury.account_party_type_mismatch", "errors.treasury.account_party_type_mismatch");

    public static readonly Error AccountMustNotCarryParty =
        Error.Validation("treasury.account_must_not_carry_party", "errors.treasury.account_must_not_carry_party");

    public static readonly Error AccountAlreadyClosed =
        Error.Conflict("treasury.account_already_closed", "errors.treasury.account_already_closed");

    public static readonly Error AccountNotClosed =
        Error.Conflict("treasury.account_not_closed", "errors.treasury.account_not_closed");

    public static readonly Error AccountNotFound =
        Error.NotFound("treasury.account_not_found", "errors.treasury.account_not_found");

    // ---- Posting ----

    public static readonly Error PostingTypeRequired =
        Error.Validation("treasury.posting_type_required", "errors.treasury.posting_type_required");

    public static readonly Error SourceDocumentTypeRequired =
        Error.Validation("treasury.source_document_type_required", "errors.treasury.source_document_type_required");

    /// <summary>A typed posting date is required; it is never defaulted (Verifier F-7). See <see cref="TypedPostingDate"/>.</summary>
    public static readonly Error PostingDateRequired =
        Error.Validation("treasury.posting_date_required", "errors.treasury.posting_date_required");

    /// <summary>A typed posting date before 2000 or past the site day. See <see cref="TypedPostingDate"/>.</summary>
    public static readonly Error PostingDateOutOfRange =
        Error.Validation("treasury.posting_date_out_of_range", "errors.treasury.posting_date_out_of_range");

    public static readonly Error AmountMustBePositive =
        Error.Validation("treasury.amount_must_be_positive", "errors.treasury.amount_must_be_positive");

    public static readonly Error SameAccountBothSides =
        Error.Validation("treasury.same_account_both_sides", "errors.treasury.same_account_both_sides");

    public static readonly Error AccountNotPostable =
        Error.Conflict("treasury.account_not_postable", "errors.treasury.account_not_postable");

    public static readonly Error AccountInactive =
        Error.Conflict("treasury.account_inactive", "errors.treasury.account_inactive");

    public static readonly Error CurrencyMismatch =
        Error.Conflict("treasury.currency_mismatch", "errors.treasury.currency_mismatch");

    /// <summary>spec.md §6.4 — the five ledgers MUST NOT be netted against each other.</summary>
    public static readonly Error LedgersMustNotNet =
        Error.Conflict("treasury.ledgers_must_not_net", "errors.treasury.ledgers_must_not_net");

    /// <summary>spec.md §5.1 — the hold only grows; nothing comes out of it before handover.</summary>
    public static readonly Error HoldOnlyGrows =
        Error.Conflict("treasury.hold_only_grows", "errors.treasury.hold_only_grows");

    /// <summary>
    /// spec.md §5.1 — the hold only grows and releases once. A posting that touches the hold is never
    /// reversed: reversing an accrual takes value out of the hold, and reversing the release puts it back
    /// for a second release (Verifier F-5, 2026-11-18). Mirrored by <c>KAFF_HOLD_REVERSAL</c>.
    /// </summary>
    public static readonly Error HoldPostingNotReversible =
        Error.Conflict("treasury.hold_posting_not_reversible", "errors.treasury.hold_posting_not_reversible");

    /// <summary>
    /// A posting a flow wrote (an extract's, an adjustment's, the handover's, a subcontractor retention release's) is
    /// reversed through that flow, which moves its document with it, and never as a generic movement (D-236). Mirrored
    /// by <c>KAFF_FLOW_REVERSAL</c>.
    /// </summary>
    public static readonly Error FlowPostingNotReversible =
        Error.Conflict("treasury.flow_posting_not_reversible", "errors.treasury.flow_posting_not_reversible");

    /// <summary>
    /// A generic movement may not name a source type a flow owns (an extract, an adjustment, the handover, a subcontractor
    /// retention release): it would count as that document's own posting (D-237, open item). Those postings are written
    /// by their flows only.
    /// </summary>
    public static readonly Error FlowSourceNotAllowed =
        Error.Validation("treasury.flow_source_not_allowed", "errors.treasury.flow_source_not_allowed");

    /// <summary>
    /// spec.md §5.1 — the hold releases in full: a release must leave it at exactly zero. Mirrors
    /// <c>KAFF_HOLD_PARTIAL_RELEASE</c>, which only the database can check.
    /// </summary>
    public static readonly Error HoldReleaseMustBeInFull =
        Error.Conflict("treasury.hold_release_in_full", "errors.treasury.hold_release_in_full");

    /// <summary>
    /// KAFF-305 — a posting type may only run between the account pair spec.md names for it. See
    /// <see cref="PostingAccountLegality"/>.
    /// </summary>
    public static readonly Error PostingTypeAccountMismatch =
        Error.Conflict("treasury.posting_type_account_mismatch", "errors.treasury.posting_type_account_mismatch");

    /// <summary>
    /// The project's state does not accept this posting type. See
    /// <see cref="PostingProjectStateLegality"/> and the ruling <c>PostingProjectStatePolicy</c>.
    /// </summary>
    public static readonly Error PostingNotAllowedInProjectState =
        Error.Conflict("treasury.posting_not_allowed_in_project_state", "errors.treasury.posting_not_allowed_in_project_state");

    /// <summary>A set of postings with no posting in it is not a movement.</summary>
    public static readonly Error PostingSetEmpty =
        Error.Validation("treasury.posting_set_empty", "errors.treasury.posting_set_empty");

    /// <summary>spec.md §6.10 — a project cost posting must name its project.</summary>
    public static readonly Error ProjectTagRequired =
        Error.Validation("treasury.project_tag_required", "errors.treasury.project_tag_required");

    /// <summary>spec.md §6.10 — a company-tagged posting must not name a project.</summary>
    public static readonly Error ProjectTagForbidden =
        Error.Validation("treasury.project_tag_forbidden", "errors.treasury.project_tag_forbidden");

    /// <summary>spec.md §6.10 — postings that touch two different projects cannot be attributed.</summary>
    public static readonly Error CrossProjectPosting =
        Error.Conflict("treasury.cross_project_posting", "errors.treasury.cross_project_posting");

    /// <summary>spec.md §6.1 — the safe balance MUST NOT go negative.</summary>
    public static readonly Error NegativeBalance =
        Error.Conflict("treasury.negative_balance", "errors.treasury.negative_balance");

    /// <summary>spec.md §6.6 — a closed period is immutable.</summary>
    public static readonly Error ClosedPeriod =
        Error.Conflict("treasury.closed_period", "errors.treasury.closed_period");

    // ---- Reversal ----

    public static readonly Error ReversalMustMirrorOriginal =
        Error.Conflict("treasury.reversal_must_mirror_original", "errors.treasury.reversal_must_mirror_original");

    public static readonly Error PostingAlreadyReversed =
        Error.Conflict("treasury.posting_already_reversed", "errors.treasury.posting_already_reversed");

    /// <summary>
    /// spec.md §6.1 — a reversal cannot itself be reversed; one correction per posting. KAFF-319:
    /// added for <c>KAFF_REVERSAL_OF_REVERSAL</c> (<c>001_guards.sql</c>), which had no domain
    /// <see cref="Error"/> until this story. KAFF-303's <c>AC-303-C</c> is expected to add the
    /// matching domain-level pre-check; this member is what it should return.
    /// </summary>
    public static readonly Error ReversalOfReversal =
        Error.Conflict("treasury.reversal_of_reversal", "errors.treasury.reversal_of_reversal");

    /// <summary>
    /// spec.md §6.1 — <c>KAFF_REVERSAL_TARGET_MISSING</c> (<c>001_guards.sql</c>): the posting named
    /// by <c>ReversesId</c> does not exist. KAFF-319 — no code path builds this today without going
    /// round the domain, but the database guard can still fire it and it must translate.
    /// </summary>
    public static readonly Error ReversalTargetNotFound =
        Error.NotFound("treasury.reversal_target_not_found", "errors.treasury.reversal_target_not_found");

    // ---- Period ----

    public static readonly Error PeriodAlreadyClosed =
        Error.Conflict("treasury.period_already_closed", "errors.treasury.period_already_closed");

    public static readonly Error PeriodRangeInvalid =
        Error.Validation("treasury.period_range_invalid", "errors.treasury.period_range_invalid");
}
