using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Kaff.Domain.Common;
using Kaff.Domain.Treasury;

namespace Kaff.Domain.Tests;

/// <summary>
/// KAFF-304. <see cref="BalanceQueries"/> is the one reusable Domain-level read over the
/// <c>account_balances</c> view. The HTTP endpoint that would expose it is blocked on Q90 (who may
/// read a ledger balance) and is not built here — see the story's own trailer and "Not in this story".
/// </summary>
/// <remarks>
/// <see cref="AccountBalance"/> is a keyless entity EF materialises by setting its private-setter
/// properties directly; it has no public constructor. <see cref="Row"/> below builds one the same way
/// EF does — via reflection over the private setters — so these tests exercise real
/// <see cref="AccountBalance"/> instances without a database. This is scoped to this test file only;
/// no production constructor was added for it.
/// </remarks>
public sealed class BalanceQueriesTests
{
    private static readonly Guid ProjectId = Guid.Parse("0195c000-0000-7000-8000-000000000010");

    // ---- AC-304-A — a single account's balance --------------------------------------------------

    /// <summary>TC-3-046.</summary>
    [Fact]
    public void GetAccountBalance_returns_inflow_outflow_raw_and_signed_matching_the_row()
    {
        Guid accountId = Guid.CreateVersion7();
        // Credit-normal account: signed = raw * -1 (rule 4).
        AccountBalance row = Row(accountId, LedgerKind.Hold, inflow: 200_000m, outflow: 60_000m, normalDebit: false);
        IQueryable<AccountBalance> source = new List<AccountBalance> { row }.AsQueryable();

        AccountBalance? result = BalanceQueries.GetAccountBalance(source, accountId);

        result.Should().NotBeNull();
        result!.Inflow.Should().Be(new Money(200_000m));
        result.Outflow.Should().Be(new Money(60_000m));
        result.RawBalance.Should().Be(new Money(140_000m));
        result.SignedBalance.Should().Be(new Money(-140_000m));
    }

    // ---- AC-304-B — five ledgers, five figures, never netted ------------------------------------

    /// <summary>TC-3-047.</summary>
    [Fact]
    public void GetLedgerBalances_returns_all_five_ledgers_plus_material_advance_as_independent_figures()
    {
        var rows = new List<AccountBalance>
        {
            Row(Guid.CreateVersion7(), LedgerKind.ClientAdvance, inflow: 250_000m, outflow: 175_000m, normalDebit: true, projectId: ProjectId),
            Row(Guid.CreateVersion7(), LedgerKind.Hold, inflow: 200_000m, outflow: 0m, normalDebit: true, projectId: ProjectId),
            Row(Guid.CreateVersion7(), LedgerKind.FirmAdvance, inflow: 0m, outflow: 0m, normalDebit: true, projectId: ProjectId),
            Row(Guid.CreateVersion7(), LedgerKind.PettyCashAdvance, inflow: 30_000m, outflow: 10_000m, normalDebit: true, projectId: ProjectId),
            Row(Guid.CreateVersion7(), LedgerKind.OwnerCurrentAccount, inflow: 50_000m, outflow: 0m, normalDebit: true, projectId: ProjectId),
            RowMaterialAdvance(Guid.CreateVersion7(), inflow: 75_000m, outflow: 45_000m, projectId: ProjectId),
            // Different project — must not leak into this project's snapshot.
            Row(Guid.CreateVersion7(), LedgerKind.Hold, inflow: 999_999m, outflow: 0m, normalDebit: true, projectId: Guid.CreateVersion7()),
        };
        IQueryable<AccountBalance> source = rows.AsQueryable();

        LedgerBalances balances = BalanceQueries.GetLedgerBalances(source, ProjectId);

        balances.ProjectId.Should().Be(ProjectId);
        balances.ClientAdvance.Should().Be(new Money(75_000m));
        balances.Hold.Should().Be(new Money(200_000m));
        balances.FirmAdvance.Should().Be(Money.Zero);
        balances.PettyCashAdvance.Should().Be(new Money(20_000m));
        balances.OwnerCurrentAccount.Should().Be(new Money(50_000m));
        balances.MaterialAdvance.Should().Be(new Money(30_000m));

        // Rule 2/3: no field anywhere sums or nets two of the five.
        typeof(LedgerBalances).GetProperties()
            .Select(property => property.Name)
            .Should().NotContain(name => name.Contains("Total", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>TC-3-048.</summary>
    [Fact]
    public void GetLedgerBalances_ledgers_missing_for_the_project_default_to_zero_not_an_exception()
    {
        var rows = new List<AccountBalance>
        {
            Row(Guid.CreateVersion7(), LedgerKind.Hold, inflow: 60_000m, outflow: 0m, normalDebit: true, projectId: ProjectId),
        };
        IQueryable<AccountBalance> source = rows.AsQueryable();

        LedgerBalances balances = BalanceQueries.GetLedgerBalances(source, ProjectId);

        balances.Hold.Should().Be(new Money(60_000m));
        balances.ClientAdvance.Should().Be(Money.Zero);
        balances.FirmAdvance.Should().Be(Money.Zero);
        balances.PettyCashAdvance.Should().Be(Money.Zero);
        balances.OwnerCurrentAccount.Should().Be(Money.Zero);
        balances.MaterialAdvance.Should().Be(Money.Zero);
    }

    // ---- AC-304-D — single aggregate, not fetch-then-sum -----------------------------------------

    /// <summary>
    /// TC-3-049. Cannot prove "one SQL aggregate query" without a live PostgreSQL — that needs an
    /// Api.Tests/Infrastructure companion against the real <c>account_balances</c> view, which does
    /// not exist yet (same caveat KAFF-300 already logs for its own database-only guards). What this
    /// case DOES check, at the Domain layer: the query object enumerates its source exactly once per
    /// call — it does not run one lookup per ledger (a five-times-round-trip "fetch-then-sum" shape)
    /// on top of a source that is itself already one query.
    /// </summary>
    [Fact]
    public void GetLedgerBalances_enumerates_its_source_exactly_once()
    {
        var rows = new List<AccountBalance>
        {
            Row(Guid.CreateVersion7(), LedgerKind.Hold, inflow: 60_000m, outflow: 0m, normalDebit: true, projectId: ProjectId),
        };
        var counting = new CountingEnumerable<AccountBalance>(rows);
        IQueryable<AccountBalance> source = counting.AsQueryable();

        BalanceQueries.GetLedgerBalances(source, ProjectId);

        counting.EnumerationCount.Should().Be(1);
    }

    /// <summary>TC-3-050. Same enumeration-count check for the single-account read.</summary>
    [Fact]
    public void GetAccountBalance_enumerates_its_source_exactly_once()
    {
        Guid accountId = Guid.CreateVersion7();
        var rows = new List<AccountBalance> { Row(accountId, LedgerKind.Hold, 1m, 0m, normalDebit: true) };
        var counting = new CountingEnumerable<AccountBalance>(rows);
        IQueryable<AccountBalance> source = counting.AsQueryable();

        BalanceQueries.GetAccountBalance(source, accountId);

        counting.EnumerationCount.Should().Be(1);
    }

    // ---- AC-304-E — zero postings, zero balance, no error ----------------------------------------

    /// <summary>TC-3-051 (Domain-level half; the HTTP half is HELD on Q90 per qa/slice-3/test-cases.md).</summary>
    [Fact]
    public void GetAccountBalance_for_an_account_with_no_postings_returns_all_zero_not_null_not_an_error()
    {
        Guid accountId = Guid.CreateVersion7();
        // The view's COALESCE guarantees zeros, never null, for an account with no postings at all.
        AccountBalance row = Row(accountId, ledgerKind: null, inflow: 0m, outflow: 0m, normalDebit: true);
        IQueryable<AccountBalance> source = new List<AccountBalance> { row }.AsQueryable();

        AccountBalance? result = BalanceQueries.GetAccountBalance(source, accountId);

        result.Should().NotBeNull();
        result!.Inflow.Should().Be(Money.Zero);
        result.Outflow.Should().Be(Money.Zero);
        result.RawBalance.Should().Be(Money.Zero);
        result.SignedBalance.Should().Be(Money.Zero);
    }

    // ---- test scaffolding --------------------------------------------------------------------------

    private static AccountBalance Row(
        Guid accountId,
        LedgerKind? ledgerKind,
        decimal inflow,
        decimal outflow,
        bool normalDebit,
        Guid? projectId = null,
        AccountType type = AccountType.ClientReceivable) =>
        Build(accountId, type, ledgerKind, inflow, outflow, normalDebit, projectId);

    private static AccountBalance RowMaterialAdvance(
        Guid accountId, decimal inflow, decimal outflow, Guid projectId) =>
        Build(accountId, AccountType.MaterialAdvance, ledgerKind: null, inflow, outflow, normalDebit: true, projectId);

    private static AccountBalance Build(
        Guid accountId,
        AccountType type,
        LedgerKind? ledgerKind,
        decimal inflow,
        decimal outflow,
        bool normalDebit,
        Guid? projectId)
    {
        var balance = (AccountBalance)RuntimeHelpers.GetUninitializedObject(typeof(AccountBalance));
        decimal raw = inflow - outflow;
        decimal signed = normalDebit ? raw : -raw;

        Set(balance, nameof(AccountBalance.AccountId), accountId);
        Set(balance, nameof(AccountBalance.AccountCode), "TEST");
        Set(balance, nameof(AccountBalance.NameAr), "حساب اختبار");
        Set(balance, nameof(AccountBalance.NameEn), "Test account");
        Set(balance, nameof(AccountBalance.Type), type);
        Set(balance, nameof(AccountBalance.Class), AccountClass.Liability);
        Set(balance, nameof(AccountBalance.NormalBalance), normalDebit ? NormalBalance.Debit : NormalBalance.Credit);
        Set(balance, nameof(AccountBalance.LedgerKind), ledgerKind);
        Set(balance, nameof(AccountBalance.ProjectId), projectId);
        Set(balance, nameof(AccountBalance.Currency), Currency.Egp);
        Set(balance, nameof(AccountBalance.Inflow), new Money(inflow));
        Set(balance, nameof(AccountBalance.Outflow), new Money(outflow));
        Set(balance, nameof(AccountBalance.RawBalance), new Money(raw));
        Set(balance, nameof(AccountBalance.SignedBalance), new Money(signed));
        Set(balance, nameof(AccountBalance.PostingCount), 0);

        return balance;
    }

    private static void Set(AccountBalance balance, string propertyName, object? value) =>
        typeof(AccountBalance)
            .GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(balance, value);

    /// <summary>Counts how many times its source is enumerated, to pin AC-304-D's "single pass" shape.</summary>
    private sealed class CountingEnumerable<T>(List<T> items) : IEnumerable<T>
    {
        public int EnumerationCount { get; private set; }

        public IEnumerator<T> GetEnumerator()
        {
            EnumerationCount++;
            return items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
