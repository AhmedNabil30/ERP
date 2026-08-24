using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Common;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests;

/// <summary>
/// The database guards, checked against a real PostgreSQL.
/// </summary>
/// <remarks>
/// spec.md §6.1: "Enforce in the database, not only in application code." Several of these tests go
/// round the domain and use raw SQL, because the question they answer is what happens when something
/// other than our C# reaches the table — a support script, a migration, a person at a psql prompt.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class TreasuryGuardTests
{
    private readonly PostgresDatabase _database;

    public TreasuryGuardTests(PostgresDatabase database) => _database = database;

    [Fact]
    public async Task A_posting_cannot_be_updated_even_by_raw_sql()
    {
        (Account safe, Account expense) = await CreateCashAndExpenseAsync();
        Posting posting = await FundAsync(safe, new Money(1_000m));
        _ = expense;

        await using KaffDbContext context = _database.CreateBareContext();

        await DatabaseGuard.RefusesAsync(
            () => context.Database.ExecuteSqlAsync($"UPDATE postings SET amount = 1 WHERE id = {posting.Id}", Ct),
            DatabaseGuard.AppendOnly);
    }

    [Fact]
    public async Task A_posting_cannot_be_deleted_even_by_raw_sql()
    {
        (Account safe, Account expense) = await CreateCashAndExpenseAsync();
        Posting posting = await FundAsync(safe, new Money(1_000m));
        _ = expense;

        await using KaffDbContext context = _database.CreateBareContext();

        await DatabaseGuard.RefusesAsync(
            () => context.Database.ExecuteSqlAsync($"DELETE FROM postings WHERE id = {posting.Id}", Ct),
            DatabaseGuard.AppendOnly);
    }

    [Fact]
    public async Task The_safe_balance_cannot_go_negative()
    {
        // spec.md §6.1: "The safe balance MUST NOT go negative. A payment that would breach this
        // fails and prompts an owner injection instead."
        (Account safe, Account expense) = await CreateCashAndExpenseAsync();
        await FundAsync(safe, new Money(1_000m));

        await using KaffDbContext context = _database.CreateContext();

        context.Postings.Add(Posting.Create(
            safe,
            expense,
            new Money(5_000m),
            PostingType.CompanyExpensePayment,
            Document(SourceDocumentType.CompanyExpense),
            Today,
            Actor,
            Now).Value);

        await DatabaseGuard.RefusesAsync(
            () => context.SaveChangesAsync(),
            DatabaseGuard.NegativeBalance);
    }

    [Fact]
    public async Task The_safe_balance_may_reach_exactly_zero()
    {
        (Account safe, Account expense) = await CreateCashAndExpenseAsync();
        await FundAsync(safe, new Money(2_000m));

        await using (KaffDbContext context = _database.CreateContext())
        {
            context.Postings.Add(Posting.Create(
                safe,
                expense,
                new Money(2_000m),
                PostingType.CompanyExpensePayment,
                Document(SourceDocumentType.CompanyExpense),
                Today,
                Actor,
                Now).Value);

            await context.SaveChangesAsync(Ct);
        }

        AccountBalance balance = await ReadBalanceAsync(safe.Id);
        balance.SignedBalance.Should().Be(Money.Zero);
    }

    [Fact]
    public async Task The_five_ledgers_cannot_be_netted_at_the_database()
    {
        Guid projectId = await CreateProjectShellAsync();
        Account hold = await AddAccountAsync(AccountType.Hold, projectId, PartyType.Client);
        Account advance = await AddAccountAsync(AccountType.ClientAdvance, projectId, PartyType.Client);

        await using KaffDbContext context = _database.CreateBareContext();

        await DatabaseGuard.RefusesAsync(
            () => InsertRawPostingAsync(context, hold.Id, advance.Id, 10_000m, nameof(PostingType.Adjustment), projectId),
            DatabaseGuard.LedgerNetting);
    }

    [Fact]
    public async Task Nothing_comes_out_of_the_hold_before_handover_at_the_database()
    {
        // spec.md §5.1: "Nothing may be taken out of it mid-project — not a snag, not a debit note,
        // not an adjustment."
        Guid projectId = await CreateProjectShellAsync();
        Account hold = await AddAccountAsync(AccountType.Hold, projectId, PartyType.Client);
        Account receivable = await AddAccountAsync(AccountType.ClientReceivable, projectId, PartyType.Client);

        await using KaffDbContext context = _database.CreateBareContext();

        await DatabaseGuard.RefusesAsync(
            () => InsertRawPostingAsync(context, hold.Id, receivable.Id, 5_000m, nameof(PostingType.DebitNote), projectId),
            DatabaseGuard.HoldDebit);
    }

    [Fact]
    public async Task The_hold_still_releases_at_handover()
    {
        Guid projectId = await CreateProjectShellAsync();
        Account hold = await AddAccountAsync(AccountType.Hold, projectId, PartyType.Client);
        Account receivable = await AddAccountAsync(AccountType.ClientReceivable, projectId, PartyType.Client);

        await using (KaffDbContext accrue = _database.CreateContext())
        {
            accrue.Postings.Add(Posting.Create(
                receivable, hold, new Money(200_000m), PostingType.HoldAccrual,
                Document(SourceDocumentType.Extract), Today, Actor, Now, projectId).Value);

            await accrue.SaveChangesAsync(Ct);
        }

        await using (KaffDbContext release = _database.CreateContext())
        {
            release.Postings.Add(Posting.Create(
                hold, receivable, new Money(200_000m), PostingType.HoldRelease,
                Document(SourceDocumentType.Extract), Today, Actor, Now, projectId).Value);

            await release.SaveChangesAsync(Ct);
        }

        AccountBalance balance = await ReadBalanceAsync(hold.Id);
        balance.SignedBalance.Should().Be(Money.Zero);
    }

    [Fact]
    public async Task A_posting_cannot_land_in_a_closed_period()
    {
        // spec.md §6.6: "a closed period is immutable."
        (Account safe, Account expense) = await CreateCashAndExpenseAsync();
        await FundAsync(safe, new Money(1_000m));

        var period = AccountingPeriod.Create(2025, 1).Value;
        period.Close(Actor, Now);

        await using (KaffDbContext setup = _database.CreateContext())
        {
            setup.AccountingPeriods.Add(period);
            await setup.SaveChangesAsync(Ct);
        }

        await using KaffDbContext context = _database.CreateContext();

        context.Postings.Add(Posting.Create(
            safe,
            expense,
            new Money(10m),
            PostingType.CompanyExpensePayment,
            Document(SourceDocumentType.CompanyExpense),
            new DateOnly(2025, 1, 15),
            Actor,
            Now).Value);

        await DatabaseGuard.RefusesAsync(
            () => context.SaveChangesAsync(),
            DatabaseGuard.ClosedPeriod);
    }

    [Fact]
    public async Task A_reversal_that_does_not_mirror_its_original_is_refused()
    {
        (Account safe, Account expense) = await CreateCashAndExpenseAsync();
        await FundAsync(safe, new Money(5_000m));

        Posting original;

        await using (KaffDbContext context = _database.CreateContext())
        {
            original = Posting.Create(
                safe, expense, new Money(1_000m), PostingType.CompanyExpensePayment,
                Document(SourceDocumentType.CompanyExpense), Today, Actor, Now).Value;

            context.Postings.Add(original);
            await context.SaveChangesAsync(Ct);
        }

        await using KaffDbContext raw = _database.CreateBareContext();

        // Right accounts, wrong amount. The domain cannot produce this; the database still refuses it.
        await DatabaseGuard.RefusesAsync(
            () => raw.Database.ExecuteSqlAsync(
                $"""
                 INSERT INTO postings
                     (id, posting_date, from_account_id, to_account_id, amount, "type",
                      source_document_type, source_document_id, source_document_reference,
                      project_id, created_by_user_id, created_at, reverses_id)
                 VALUES
                     ({Guid.CreateVersion7()}, {Today}, {expense.Id}, {safe.Id}, {500m},
                      {nameof(PostingType.CompanyExpensePayment)},
                      {nameof(SourceDocumentType.CompanyExpense)}, {Guid.CreateVersion7()}, NULL,
                      NULL, {Actor}, {Now}, {original.Id})
                 """,
                Ct),
            DatabaseGuard.ReversalMismatch);
    }

    [Fact]
    public async Task Balances_come_from_the_view_and_reconcile()
    {
        (Account safe, Account expense) = await CreateCashAndExpenseAsync();
        await FundAsync(safe, new Money(7_500m));
        _ = expense;

        AccountBalance balance = await ReadBalanceAsync(safe.Id);

        balance.Inflow.Should().Be(new Money(7_500m));
        balance.Outflow.Should().Be(Money.Zero);
        balance.RawBalance.Should().Be(new Money(7_500m));
        balance.SignedBalance.Should().Be(new Money(7_500m));
        balance.PostingCount.Should().Be(1);
    }

    // ---- helpers -----------------------------------------------------------------------------

    /// <summary>
    /// The ambient test cancellation token. Threaded through every database call so a test that
    /// blocks — on the advisory locks the balance guard takes, for instance — is cancelled by the
    /// runner rather than hanging the suite.
    /// </summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Today => new(2026, 6, 1);

    private static DateTimeOffset Now => new(2026, 6, 1, 8, 0, 0, TimeSpan.Zero);

    private static Guid Actor => Guid.Parse("0195c000-0000-7000-8000-0000000000ff");

    private static SourceDocument Document(SourceDocumentType type) =>
        new(type, Guid.CreateVersion7(), null);

    private async Task<AccountBalance> ReadBalanceAsync(Guid accountId)
    {
        await using KaffDbContext context = _database.CreateBareContext();

        return await context.AccountBalances.SingleAsync(balance => balance.AccountId == accountId, Ct);
    }

    private async Task<(Account Safe, Account Expense)> CreateCashAndExpenseAsync()
    {
        Account safe = await AddAccountAsync(AccountType.Safe);
        Account expense = await AddAccountAsync(AccountType.CompanyExpense);
        return (safe, expense);
    }

    /// <summary>
    /// Puts money in the safe so it has something to spend.
    /// </summary>
    /// <remarks>
    /// Funded from paid-in capital rather than the owner current account, because spec.md §6.4.5
    /// treats جاري المالك as a single company-wide account and the database enforces that with a
    /// unique index — each test needs its own funding account.
    /// </remarks>
    private async Task<Posting> FundAsync(Account safe, Money amount)
    {
        Account capital = await AddAccountAsync(AccountType.PaidInCapital);

        await using KaffDbContext context = _database.CreateContext();

        Posting funding = Posting.Create(
            capital,
            safe,
            amount,
            PostingType.OpeningBalance,
            Document(SourceDocumentType.OpeningBalance),
            Today,
            Actor,
            Now).Value;

        context.Postings.Add(funding);
        await context.SaveChangesAsync(Ct);

        return funding;
    }

    private async Task<Account> AddAccountAsync(AccountType type, Guid? projectId = null, PartyType? partyType = null)
    {
        Result<Account> created = Account.Create(
            type,
            $"T{(int)type}-{Guid.CreateVersion7():N}"[..20],
            "حساب اختبار",
            "Test account",
            Currency.Egp,
            new DateOnly(2026, 1, 1),
            projectId,
            partyType,
            partyType is null ? null : Guid.CreateVersion7());

        created.IsSuccess.Should().BeTrue();

        await using KaffDbContext context = _database.CreateContext();
        context.Accounts.Add(created.Value);
        await context.SaveChangesAsync(Ct);

        return created.Value;
    }

    /// <summary>
    /// A project row, needed only so project-scoped accounts have a valid foreign key. The billing
    /// side of projects is slice 4; nothing here depends on it.
    /// </summary>
    private async Task<Guid> CreateProjectShellAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        Domain.MasterData.Client client = Domain.MasterData.Client.Create(
            $"C{Guid.CreateVersion7():N}"[..12],
            "عميل اختبار",
            UniqueNames.Phone(),
            Domain.MasterData.ClientKind.Corporate,
            Now).Value;

        Domain.Projects.Project project = Domain.Projects.Project.Create(
            $"P{Guid.CreateVersion7():N}"[..12],
            "مشروع اختبار",
            client.Id,
            Domain.Contracts.ContractType.LumpSum,
            Now).Value;

        context.Clients.Add(client);
        context.Projects.Add(project);
        await context.SaveChangesAsync(Ct);

        return project.Id;
    }

    private static Task<int> InsertRawPostingAsync(
        KaffDbContext context,
        Guid fromAccountId,
        Guid toAccountId,
        decimal amount,
        string postingType,
        Guid? projectId)
        => context.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO postings
                 (id, posting_date, from_account_id, to_account_id, amount, "type",
                  source_document_type, source_document_id, source_document_reference,
                  project_id, created_by_user_id, created_at, reverses_id)
             VALUES
                 ({Guid.CreateVersion7()}, {Today}, {fromAccountId}, {toAccountId}, {amount}, {postingType},
                  {nameof(SourceDocumentType.Adjustment)}, {Guid.CreateVersion7()}, NULL,
                  {projectId}, {Actor}, {Now}, NULL)
             """,
            Ct);
}
