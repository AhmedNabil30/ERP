using Kaff.Domain.Common;

namespace Kaff.Domain.Treasury;

/// <summary>
/// A derived balance. Read-only, keyless, and backed by the <c>account_balances</c> database view.
/// </summary>
/// <remarks>
/// <para>
/// CLAUDE.md: "Never store a balance. Balances are derived by summing postings, always. If you find
/// yourself adding a <c>Balance</c> column, stop — that's the bug."
/// </para>
/// <para>
/// A view rather than a query helper, for two reasons. It is derivation expressed in the one place
/// that cannot be bypassed — there is no balance column anywhere in the schema for anything to drift
/// from. And the summation happens in PostgreSQL over an index, so a balance stays a single
/// aggregate rather than materialising a project's postings into memory.
/// </para>
/// <para>
/// <see cref="RawBalance"/> is inflow minus outflow. <see cref="SignedBalance"/> multiplies that by
/// the account's normal direction, so a liability with money owed on it reads positive. Reports use
/// <see cref="SignedBalance"/>; the non-negative database guard checks it too.
/// </para>
/// </remarks>
public sealed class AccountBalance
{
    public Guid AccountId { get; private set; }

    public string AccountCode { get; private set; } = null!;

    public string NameAr { get; private set; } = null!;

    public string NameEn { get; private set; } = null!;

    public AccountType Type { get; private set; }

    public AccountClass Class { get; private set; }

    public NormalBalance NormalBalance { get; private set; }

    /// <summary>Set for the five ledgers of spec.md §6.4. Never aggregate across differing values.</summary>
    public LedgerKind? LedgerKind { get; private set; }

    public Guid? ProjectId { get; private set; }

    public PartyType? PartyType { get; private set; }

    public Guid? PartyId { get; private set; }

    public Currency Currency { get; private set; }

    /// <summary>Total moved into the account.</summary>
    public Money Inflow { get; private set; }

    /// <summary>Total moved out of the account.</summary>
    public Money Outflow { get; private set; }

    /// <summary>Inflow minus outflow, without regard to the account's normal direction.</summary>
    public Money RawBalance { get; private set; }

    /// <summary>Balance in the account's own direction. Positive means "normal".</summary>
    public Money SignedBalance { get; private set; }

    public int PostingCount { get; private set; }

    public DateOnly? LastPostingDate { get; private set; }
}

/// <summary>
/// The five ledgers of spec.md §6.4, reported as five separate figures for one project.
/// </summary>
/// <remarks>
/// The type exists to make netting hard to write by accident. There is no <c>Total</c> property and
/// there must never be one: CLAUDE.md forbids any calculation that offsets one of these against
/// another. A report that needs to show them together shows five rows, not a sum.
///
/// تشوينات is carried alongside because spec.md §15 tracks it to zero on the same statement, but it
/// is not one of the five and is not subject to the netting prohibition.
/// </remarks>
public sealed record LedgerBalances(
    Guid ProjectId,
    Money ClientAdvance,
    Money Hold,
    Money FirmAdvance,
    Money PettyCashAdvance,
    Money OwnerCurrentAccount,
    Money MaterialAdvance);
