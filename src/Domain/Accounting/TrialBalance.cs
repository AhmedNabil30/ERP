using Kaff.Domain.Common;
using Kaff.Domain.Treasury;

namespace Kaff.Domain.Accounting;

/// <summary>One account as the trial balance needs it.</summary>
public sealed record TrialBalanceAccount(
    Guid AccountId,
    string Code,
    string NameAr,
    string NameEn,
    AccountType Type,
    AccountClass Class,
    NormalBalance NormalBalance,
    Guid? ProjectId,
    LedgerKind? LedgerKind);

/// <summary>One line of the trial balance: the account's net balance in the debit or the credit column, never both.</summary>
public sealed record TrialBalanceRow(TrialBalanceAccount Account, Money Debit, Money Credit);

public sealed record TrialBalanceResult(IReadOnlyList<TrialBalanceRow> Rows, Money TotalDebit, Money TotalCredit)
{
    public bool IsBalanced => TotalDebit == TotalCredit;
}

/// <summary>
/// The trial balance of spec.md section 6.6: every account's balance derived from postings, shown in one of two columns,
/// with the two column totals equal. KAFF-704.
/// </summary>
/// <remarks>
/// A posting leaves one account (credit) and lands in another (debit), so the column totals are equal by construction. An
/// account whose net is debit goes in the debit column, whatever its normal balance, so an abnormal balance shows on the
/// side it really sits. A contra account such as accumulated depreciation normally lands in the credit column. Every account
/// is its own row: the five ledgers never net against each other (spec.md section 6.4).
/// </remarks>
public static class TrialBalance
{
    /// <param name="accounts">The accounts to list.</param>
    /// <param name="debits">Per account, the sum of postings that landed in it.</param>
    /// <param name="credits">Per account, the sum of postings that left it.</param>
    public static TrialBalanceResult Build(
        IEnumerable<TrialBalanceAccount> accounts,
        IReadOnlyDictionary<Guid, Money> debits,
        IReadOnlyDictionary<Guid, Money> credits)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(debits);
        ArgumentNullException.ThrowIfNull(credits);

        List<TrialBalanceRow> rows = [];

        foreach (TrialBalanceAccount account in accounts.OrderBy(candidate => candidate.Code, StringComparer.Ordinal))
        {
            bool hasDebits = debits.TryGetValue(account.AccountId, out Money debit);
            bool hasCredits = credits.TryGetValue(account.AccountId, out Money credit);

            if (!hasDebits && !hasCredits)
            {
                continue;
            }

            Money net = debit - credit;

            rows.Add(net.IsNegative
                ? new TrialBalanceRow(account, Money.Zero, -net)
                : new TrialBalanceRow(account, net, Money.Zero));
        }

        return new TrialBalanceResult(
            rows,
            Money.Sum(rows.Select(row => row.Debit)),
            Money.Sum(rows.Select(row => row.Credit)));
    }
}
