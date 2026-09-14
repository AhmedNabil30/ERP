using Kaff.Domain.Common;

namespace Kaff.Domain.Treasury;

/// <summary>
/// The one query surface for a derived balance (AC-304-C). Every caller — a hold-release check, an
/// extract screen, a عهدة balance check — reads through here, never by writing its own LINQ against
/// <c>account_balances</c> directly.
/// </summary>
/// <remarks>
/// Takes <see cref="IQueryable{T}"/> rather than a <c>DbContext</c> so <c>Kaff.Domain</c> stays free
/// of an EF Core reference (see <c>Kaff.Domain.csproj</c>: "No EF Core, no ASP.NET Core"). The caller
/// passes <c>context.AccountBalances</c> — the keyless entity already mapped onto the
/// <c>account_balances</c> view (spec.md §6.1; <c>002_views.sql</c>).
/// <para>
/// Both methods below enumerate their source exactly once (AC-304-D): <see cref="GetAccountBalance"/>
/// is a single filtered lookup, and <see cref="GetLedgerBalances"/> pulls the project's few
/// already-aggregated rows in one pass and then reads them out of that in-memory list — it never
/// issues one query per ledger. The view's own <c>COALESCE(..., 0)</c> is what makes a zero-posting
/// account come back zero rather than null or an error (AC-304-E); this class does not reintroduce
/// null handling on top of that.
/// </para>
/// </remarks>
public static class BalanceQueries
{
    /// <summary>
    /// AC-304-A, AC-304-E. Null only if <paramref name="accountId"/> names no account at all — every
    /// real account has exactly one row in the view, zero-valued if it has no postings.
    /// </summary>
    public static AccountBalance? GetAccountBalance(IQueryable<AccountBalance> balances, Guid accountId) =>
        balances.SingleOrDefault(balance => balance.AccountId == accountId);

    /// <summary>
    /// AC-304-B. The five ledgers of spec.md §6.4 plus تشوينات, as five-plus-one independent figures.
    /// No field here sums or nets any two of them — see <see cref="LedgerBalances"/>'s own remarks.
    /// </summary>
    public static LedgerBalances GetLedgerBalances(IQueryable<AccountBalance> balances, Guid projectId)
    {
        List<AccountBalance> rows = balances
            .Where(balance => balance.ProjectId == projectId
                && (balance.LedgerKind != null || balance.Type == AccountType.MaterialAdvance))
            .ToList();

        Money ForLedger(LedgerKind kind) =>
            rows.SingleOrDefault(balance => balance.LedgerKind == kind)?.SignedBalance ?? Money.Zero;

        Money materialAdvance = rows
            .SingleOrDefault(balance => balance.Type == AccountType.MaterialAdvance)?.SignedBalance
            ?? Money.Zero;

        return new LedgerBalances(
            projectId,
            ForLedger(LedgerKind.ClientAdvance),
            ForLedger(LedgerKind.Hold),
            ForLedger(LedgerKind.FirmAdvance),
            ForLedger(LedgerKind.PettyCashAdvance),
            ForLedger(LedgerKind.OwnerCurrentAccount),
            materialAdvance);
    }
}
