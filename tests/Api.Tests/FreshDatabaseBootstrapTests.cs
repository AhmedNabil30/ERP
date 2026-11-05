using System.Text.RegularExpressions;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Common;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace Kaff.Api.Tests;

/// <summary>
/// A genuinely empty database, brought up through the application's own start-up path
/// (<see cref="DatabaseInitializer.InitialiseAsync"/> with <see cref="SchemaStrategy.Migrate"/>), then checked
/// for the guards that make the money rules true. See decisions.md D-203.
/// </summary>
/// <remarks>
/// Every other test class gets its schema from <c>EnsureCreated</c> (<see cref="PostgresDatabase"/>, and
/// <c>KaffApiFactory</c> through it), so no migration ever ran in the suite and a migration that could not
/// run on an empty database shipped unnoticed. This class is the only place migrations run.
/// </remarks>
public sealed partial class FreshDatabaseBootstrapTests : IAsyncLifetime
{
    private static readonly string AdminConnectionString =
        Environment.GetEnvironmentVariable("KAFF_TEST_DB")
        ?? "Host=localhost;Port=5432;Database=kaff;Username=kaff;Password=kaff;Include Error Detail=true";

    private static SemaphoreSlim Gate { get; } = new(1, 1);

    private readonly string _name = $"kaff_fresh_{Guid.CreateVersion7():N}";
    private readonly string _migrateOnlyName = $"kaff_fresh_{Guid.CreateVersion7():N}";
    private string _connectionString = string.Empty;
    private string _migrateOnlyConnectionString = string.Empty;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Today => new(2026, 6, 1);

    private static DateTimeOffset Now => new(2026, 6, 1, 8, 0, 0, TimeSpan.Zero);

    private static Guid Actor => Guid.Parse("0195c000-0000-7000-8000-0000000000ff");

    public async ValueTask InitializeAsync()
    {
        await ExecuteAdminAsync($"CREATE DATABASE \"{_name}\"");
        await ExecuteAdminAsync($"CREATE DATABASE \"{_migrateOnlyName}\"");
        _migrateOnlyConnectionString = new NpgsqlConnectionStringBuilder(AdminConnectionString) { Database = _migrateOnlyName }.ConnectionString;
        _connectionString = new NpgsqlConnectionStringBuilder(AdminConnectionString) { Database = _name }.ConnectionString;

        await using KaffDbContext context = CreateContext();
        var initializer = new DatabaseInitializer(context, NullLogger<DatabaseInitializer>.Instance);

        // The guard script revokes privileges from a cluster-wide role; see PostgresDatabase.InitialisationGate.
        await Gate.WaitAsync(Ct);
        try
        {
            await initializer.InitialiseAsync(SchemaStrategy.Migrate, Ct);

            // Migrations alone, as `dotnet ef database update` runs them, with no start-up guard pass to mask a
            // guard that no migration installs.
            await using var migrateOnly = new KaffDbContext(
                new DbContextOptionsBuilder<KaffDbContext>().UseNpgsql(_migrateOnlyConnectionString).Options);
            await migrateOnly.Database.MigrateAsync(Ct);
        }
        finally
        {
            Gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await ExecuteAdminAsync($"DROP DATABASE IF EXISTS \"{_name}\" WITH (FORCE)");
        await ExecuteAdminAsync($"DROP DATABASE IF EXISTS \"{_migrateOnlyName}\" WITH (FORCE)");
    }

    [Fact]
    public async Task Start_up_guard_check_finds_nothing_missing_on_a_migrated_empty_database()
    {
        await using KaffDbContext context = CreateContext();
        var initializer = new DatabaseInitializer(context, NullLogger<DatabaseInitializer>.Instance);

        (await initializer.FindMissingGuardsAsync(Ct)).Should().BeEmpty();
    }

    [Fact]
    public async Task Every_trigger_and_function_in_every_guard_script_exists_after_migrating()
    {
        // Goes red if any script is skipped, because it reads the scripts themselves.
        List<string> missing = [];

        foreach (string connectionString in new[] { _connectionString, _migrateOnlyConnectionString })
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(Ct);

            foreach (string script in GuardScripts.ReadAllInOrder())
            {
                foreach (Match match in TriggerPattern().Matches(script))
                {
                    if (!await ExistsAsync(connection, "select 1 from pg_trigger where tgname = @n and not tgisinternal", match.Groups[1].Value))
                    {
                        missing.Add("trigger " + match.Groups[1].Value);
                    }
                }

                foreach (Match match in FunctionPattern().Matches(script))
                {
                    if (!await ExistsAsync(connection, "select 1 from pg_proc where proname = @n", match.Groups[1].Value))
                    {
                        missing.Add("function " + match.Groups[1].Value);
                    }
                }
            }
        }

        missing.Should().BeEmpty();
    }

    [Fact]
    public async Task The_safe_balance_cannot_go_negative_on_a_migrated_database()
    {
        Account safe = await AddAccountAsync(AccountType.Safe);
        Account expense = await AddAccountAsync(AccountType.CompanyExpense);
        Account capital = await AddAccountAsync(AccountType.PaidInCapital);

        await using KaffDbContext context = CreateContext();
        context.Postings.Add(Posting.Create(
            capital, safe, new Money(1_000m), PostingType.OpeningBalance,
            new SourceDocument(SourceDocumentType.OpeningBalance, Guid.CreateVersion7(), null), Today, Actor, Now).Value);
        await context.SaveChangesAsync(Ct);

        context.Postings.Add(Posting.Create(
            safe, expense, new Money(5_000m), PostingType.CompanyExpensePayment,
            new SourceDocument(SourceDocumentType.CompanyExpense, Guid.CreateVersion7(), null), Today, Actor, Now).Value);

        await DatabaseGuard.RefusesAsync(() => context.SaveChangesAsync(Ct), DatabaseGuard.NegativeBalance);
    }

    [Fact]
    public async Task Nothing_comes_out_of_the_hold_on_a_migrated_database()
    {
        Guid projectId;
        await using (KaffDbContext seed = CreateContext())
        {
            Domain.MasterData.Client client = Domain.MasterData.Client.Create(
                $"C{Guid.CreateVersion7():N}"[..12], "عميل اختبار", UniqueNames.Phone(),
                Domain.MasterData.ClientKind.Corporate, Now).Value;
            Domain.Projects.Project project = Domain.Projects.Project.Create(
                $"P{Guid.CreateVersion7():N}"[..12], "مشروع اختبار", client.Id,
                Domain.Contracts.ContractType.LumpSum, Now).Value;
            seed.Clients.Add(client);
            seed.Projects.Add(project);
            await seed.SaveChangesAsync(Ct);
            projectId = project.Id;
        }

        Account hold = await AddAccountAsync(AccountType.Hold, projectId, PartyType.Client);
        Account receivable = await AddAccountAsync(AccountType.ClientReceivable, projectId, PartyType.Client);

        await using KaffDbContext context = CreateContext();
        await DatabaseGuard.RefusesAsync(
            () => context.Database.ExecuteSqlAsync(
                $"""
                 INSERT INTO postings
                     (id, posting_date, from_account_id, to_account_id, amount, "type",
                      source_document_type, source_document_id, source_document_reference,
                      project_id, created_by_user_id, created_at, reverses_id)
                 VALUES
                     ({Guid.CreateVersion7()}, {Today}, {hold.Id}, {receivable.Id}, {5_000m}, {nameof(PostingType.DebitNote)},
                      {nameof(SourceDocumentType.Adjustment)}, {Guid.CreateVersion7()}, NULL,
                      {projectId}, {Actor}, {Now}, NULL)
                 """,
                Ct),
            DatabaseGuard.HoldDebit);
    }

    [Fact]
    public async Task The_boq_freeze_guard_is_installed_by_migrations_alone()
    {
        await using var context = new KaffDbContext(
            new DbContextOptionsBuilder<KaffDbContext>().UseNpgsql(_migrateOnlyConnectionString).Options);

        await DatabaseGuard.RefusesAsync(
            () => context.Database.ExecuteSqlRawAsync("TRUNCATE boq_lines", Ct),
            "KAFF_BOQ_FROZEN");
    }

    [GeneratedRegex(@"CREATE\s+TRIGGER\s+(\w+)", RegexOptions.IgnoreCase)]
    private static partial Regex TriggerPattern();

    [GeneratedRegex(@"CREATE\s+(?:OR\s+REPLACE\s+)?FUNCTION\s+(\w+)", RegexOptions.IgnoreCase)]
    private static partial Regex FunctionPattern();

    private static async Task ExecuteAdminAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(AdminConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(Ct);
    }

    private static async Task<bool> ExistsAsync(NpgsqlConnection connection, string sql, string name)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("n", name);
        return await command.ExecuteScalarAsync(Ct) is not null;
    }

    private KaffDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<KaffDbContext>().UseNpgsql(_connectionString).Options);

    private async Task<Account> AddAccountAsync(AccountType type, Guid? projectId = null, PartyType? partyType = null)
    {
        Account account = Account.Create(
            type, $"T{(int)type}-{Guid.CreateVersion7():N}"[..20], "حساب اختبار", "Test account",
            Currency.Egp, new DateOnly(2026, 1, 1), projectId, partyType,
            partyType is null ? null : Guid.CreateVersion7()).Value;

        await using KaffDbContext context = CreateContext();
        context.Accounts.Add(account);
        await context.SaveChangesAsync(Ct);
        return account;
    }
}
