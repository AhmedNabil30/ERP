using Kaff.Domain.Common;

namespace Kaff.Api.Features.Treasury.GetBalances;

/// <summary>
/// The five ledgers of spec.md §6.4, side by side, plus تشوينات. KAFF-306, AC-306-D.
/// </summary>
/// <remarks>
/// <b>Deliberately no <c>Total</c> field, and none may be added.</b> CLAUDE.md: "The five ledgers
/// never net against each other." A summed figure here would be exactly the field this rule forbids —
/// see <c>Kaff.Domain.Treasury.LedgerBalances</c>'s own remarks, which this record mirrors field for
/// field.
/// </remarks>
public sealed record LedgerBalancesResponse(
    Guid ProjectId,
    Money ClientAdvance,
    Money Hold,
    Money FirmAdvance,
    Money PettyCashAdvance,
    Money OwnerCurrentAccount,
    Money MaterialAdvance);

/// <summary>One account's derived balance. KAFF-306, S-036.</summary>
/// <param name="RawBalance">Inflow minus outflow, ignoring the account's own direction.</param>
/// <param name="SignedBalance">Balance in the account's own direction — what the non-negative guard checks.</param>
public sealed record AccountBalanceResponse(
    Guid AccountId,
    string AccountCode,
    string NameAr,
    string NameEn,
    Money Inflow,
    Money Outflow,
    Money RawBalance,
    Money SignedBalance,
    int PostingCount,
    DateOnly? LastPostingDate);
