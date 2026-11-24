using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Kaff.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kaff.Api.Tests;

/// <summary>
/// The company accounts the accounting forms pick from (Nabil, 2026-11-24): the seeder creates the accrued-expense and
/// prepaid-expense accounts once, and a second run adds nothing. It runs here, on a fresh database, because the shared test
/// database holds company ledger accounts other tests create under other codes, which the seeder's own inserts would collide with.
/// </summary>
public sealed partial class FreshDatabaseBootstrapTests
{
    [Fact]
    public async Task The_seeder_creates_the_accrued_and_prepaid_expense_accounts_once()
    {
        await using var context = new KaffDbContext(
            new DbContextOptionsBuilder<KaffDbContext>().UseNpgsql(_ownerConnectionString).Options);
        var seeder = new AccountTreeSeeder(context, TimeProvider.System, NullLogger<AccountTreeSeeder>.Instance);

        await seeder.SeedAsync(Ct);
        (await seeder.SeedAsync(Ct)).Should().Be(0, "a second run inserts nothing");

        List<Account> found = await context.Accounts.AsNoTracking()
            .Where(account => account.Code == "ASSET-PREPAID" || account.Code == "LIAB-ACCRUED")
            .ToListAsync(Ct);
        found.Should().HaveCount(2);
        found.Single(account => account.Code == "ASSET-PREPAID").Should().Match<Account>(account =>
            account.Type == AccountType.Prepayment && account.NameAr == "مصروفات مدفوعة مقدماً" && account.NameEn == "Prepaid expenses" && account.ProjectId == null);
        found.Single(account => account.Code == "LIAB-ACCRUED").Should().Match<Account>(account =>
            account.Type == AccountType.AccruedExpense && account.NameAr == "مصروفات مستحقة" && account.NameEn == "Accrued expenses" && account.ProjectId == null);
    }
}
