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

    // ---- Posting ----

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

    // ---- Period ----

    public static readonly Error PeriodAlreadyClosed =
        Error.Conflict("treasury.period_already_closed", "errors.treasury.period_already_closed");

    public static readonly Error PeriodRangeInvalid =
        Error.Validation("treasury.period_range_invalid", "errors.treasury.period_range_invalid");
}
