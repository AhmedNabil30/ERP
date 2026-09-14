using System.Collections.Frozen;
using Kaff.Domain.Common;

namespace Kaff.Domain.Treasury;

/// <summary>
/// The posting-type × account-pair legality table. KAFF-305.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two rules, both spec.md-cited, no others.</b> Rules 2-7 (a small set of restricted ledgers may
/// only be touched by the posting types spec.md names for them) and rule 8 (a non-cash posting type
/// must never touch a cash instrument). Everything else a <see cref="PostingType"/> might legally
/// touch is unconstrained by this table — not because it is safe, but because spec.md, decisions.md
/// and <see cref="PostingType"/>'s own remarks do not name a pair for it. Building a positive
/// allow-list for those types would be inventing a business rule, which CLAUDE.md forbids ("If
/// spec.md doesn't answer a business question, stop and ask. Do not decide.").
/// </para>
/// <para>
/// <see cref="PendingQ91"/> is the honest name for that gap: every <see cref="PostingType"/> spec.md
/// does not name a pair for, tracked as data so <c>Q91</c> has a concrete list to answer rather than a
/// vague "some types" — the same discipline <see cref="PostingTypes"/> applies to cash/non-cash.
/// Adding or removing a member here is a deliberate act reviewed against spec.md (rule 10), not a
/// data-entry operation.
/// </para>
/// </remarks>
public static class PostingAccountLegality
{
    /// <summary>
    /// Rules 2-7: an <see cref="AccountType"/> that only a named handful of <see cref="PostingType"/>s
    /// may touch, from either side of the posting.
    /// </summary>
    private static readonly FrozenDictionary<AccountType, FrozenSet<PostingType>> RestrictedLedgerAccounts =
        new Dictionary<AccountType, FrozenSet<PostingType>>
        {
            // Rule 2 — spec.md §5.1: only HoldAccrual moves value in, only HoldRelease moves it out.
            // Direction itself is enforced separately by Posting's own HoldOnlyGrows check; this is
            // the type-membership half of the same rule, and it applies to a reversal exactly as it
            // does to a forward posting (rule 9 — no special exemption).
            [AccountType.Hold] = FrozenSet.ToFrozenSet([PostingType.HoldAccrual, PostingType.HoldRelease]),

            // Rule 3 — spec.md §5.1, §15, D-034.
            [AccountType.MaterialAdvance] = FrozenSet.ToFrozenSet(
                [PostingType.MaterialAdvanceIssue, PostingType.MaterialAdvanceRecovery]),

            // Rule 4 — spec.md §15.
            [AccountType.ClientAdvance] = FrozenSet.ToFrozenSet(
                [PostingType.ClientAdvanceReceipt, PostingType.ClientAdvanceRecovery]),

            // Rule 5 — spec.md §6.4.3.
            [AccountType.FirmAdvance] = FrozenSet.ToFrozenSet(
                [PostingType.FirmAdvanceIssue, PostingType.FirmAdvanceRecovery]),

            // Rule 6 — spec.md §6.4.5.
            [AccountType.OwnerCurrentAccount] = FrozenSet.ToFrozenSet(
                [PostingType.OwnerInjection, PostingType.OwnerWithdrawal, PostingType.OwnerRepayment, PostingType.OwnerDrawing]),

            // Rule 7 — spec.md §6.4.4.
            [AccountType.PettyCashAdvance] = FrozenSet.ToFrozenSet(
                [PostingType.PettyCashIssue, PostingType.PettyCashSettlement, PostingType.PettyCashReturn]),
        }.ToFrozenDictionary();

    /// <summary>
    /// Rule 1/10 as data. Every <see cref="PostingType"/> that spec.md, decisions.md and
    /// <see cref="PostingType"/>'s own remark do not name an account pair for.
    /// </summary>
    /// <remarks>
    /// <para><b>Named by the story (KAFF-305, Q91)</b> — Karim/Architect input outstanding:</para>
    /// <list type="bullet">
    /// <item><see cref="PostingType.SiteExpensePayment"/> — spec.md §8/§6.10 names the event, not the
    /// account it lands on; <see cref="AccountType.ProjectCost"/> is a plausible but unconfirmed target.</item>
    /// <item><see cref="PostingType.AssetPurchase"/> — spec.md §6.6 names the event; whether the other
    /// side is always cash or sometimes a loan is unconfirmed.</item>
    /// <item><see cref="PostingType.LoanDrawdown"/> — spec.md §6.6, assumption 16 🟡; the financing
    /// model itself is an assumption, not a confirmed rule.</item>
    /// <item><see cref="PostingType.PeriodCloseTransfer"/> — spec.md §6.6 names no specific pair.</item>
    /// <item><see cref="PostingType.YearEndProfitTransfer"/> — spec.md §6.6; CurrentYearProfit and
    /// RetainedEarnings exist and look obvious, but the story flags it anyway and this table does not
    /// override that ruling with its own inference.</item>
    /// </list>
    /// <para><b>Found by this story's own exhaustiveness sweep</b> (AC-305-F), reported back through
    /// Q91 rather than decided here:</para>
    /// <list type="bullet">
    /// <item><see cref="PostingType.OpeningBalance"/> — every account is seeded at go-live; there is
    /// no single named counter-account, and none of the 34 <see cref="AccountType"/> members is an
    /// opening-balance suspense/equity account.</item>
    /// <item><see cref="PostingType.ChequeDeposit"/>, <see cref="PostingType.ChequeClearance"/>,
    /// <see cref="PostingType.ChequeBounce"/> — no <see cref="AccountType"/> represents a cheque in
    /// hand or in transit; the 34-member catalogue has nowhere for these three to land.</item>
    /// </list>
    /// <para>
    /// <see cref="PostingType.Adjustment"/> is deliberately NOT on this list. Its own remark says it is
    /// a catch-all by design ("Any other movement covered by the single Adjustment object"), which is
    /// a stated property, not a gap — rule 8 still bounds it away from cash.
    /// </para>
    /// </remarks>
    public static readonly FrozenSet<PostingType> PendingQ91 = FrozenSet.ToFrozenSet(
    [
        PostingType.OpeningBalance,
        PostingType.ChequeDeposit,
        PostingType.ChequeClearance,
        PostingType.ChequeBounce,
        PostingType.SiteExpensePayment,
        PostingType.AssetPurchase,
        PostingType.LoanDrawdown,
        PostingType.PeriodCloseTransfer,
        PostingType.YearEndProfitTransfer,
    ]);

    /// <summary>
    /// Rules 2-8, applied to one posting. Called by <see cref="Posting"/> for both a forward posting
    /// and a reversal — a reversal passes the swapped account pair and its own (inherited) type, with
    /// no exemption (rule 9).
    /// </summary>
    public static Result Validate(AccountType from, AccountType to, PostingType type)
    {
        foreach ((AccountType restricted, FrozenSet<PostingType> allowed) in RestrictedLedgerAccounts)
        {
            if ((from == restricted || to == restricted) && !allowed.Contains(type))
            {
                return Result.Failure(TreasuryErrors.PostingTypeAccountMismatch);
            }
        }

        // Rule 8 — spec.md §6.2: a non-cash posting type must never touch a cash instrument.
        if (PostingTypes.IsNonCash(type) && (from is AccountType.Safe or AccountType.Bank || to is AccountType.Safe or AccountType.Bank))
        {
            return Result.Failure(TreasuryErrors.PostingTypeAccountMismatch);
        }

        return Result.Success();
    }
}
