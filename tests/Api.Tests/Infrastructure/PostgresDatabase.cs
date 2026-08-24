using Kaff.Domain.Auditing;
using Kaff.Domain.Identity;
using Kaff.Infrastructure.Auditing;
using Kaff.Infrastructure.Persistence;
using Kaff.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace Kaff.Api.Tests.Infrastructure;

/// <summary>
/// A real PostgreSQL database, created for the test run and dropped afterwards.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a real database.</b> The rules that matter most here live in PostgreSQL: append-only
/// triggers, the non-negative balance guard, the ledger-netting prohibition, the closed-period check,
/// the balances view. An in-memory or SQLite provider would run every test green while enforcing none
/// of them, which is worse than having no tests — it would report safety that does not exist.
/// </para>
/// <para>
/// <b>Why not Testcontainers.</b> It would work, but it makes Docker a hard requirement for running
/// the suite. A connection string does not, and CI service containers provide one for free.
/// Point <c>KAFF_TEST_DB</c> at any PostgreSQL 14+ server; the fixture creates and drops its own
/// database, so it never touches an existing one. See decisions.md D-022.
/// </para>
/// </remarks>
public sealed class PostgresDatabase : IAsyncLifetime
{
    private const string EnvironmentVariable = "KAFF_TEST_DB";

    /// <summary>
    /// Used when <c>KAFF_TEST_DB</c> is unset. Matches <c>docker-compose.yml</c>, which is the only
    /// database this repository actually creates.
    /// </summary>
    /// <remarks>
    /// This said <c>postgres/postgres</c> until 2026-08-22 — credentials the compose file never
    /// creates — so running the suite without the variable failed on <c>28P01: password
    /// authentication failed</c>, and the error message below then recommended the same wrong
    /// credentials. CI is unaffected: its service container really is <c>postgres/postgres</c> and it
    /// sets the variable explicitly. See decisions.md D-054.
    /// </remarks>
    private const string DefaultAdminConnectionString =
        "Host=localhost;Port=5432;Database=kaff;Username=kaff;Password=kaff;Include Error Detail=true";

    private string _adminConnectionString = DefaultAdminConnectionString;
    private string _databaseName = string.Empty;

    /// <summary>
    /// Set only after CREATE DATABASE succeeds.
    /// </summary>
    /// <remarks>
    /// Without it, a failure to connect during initialisation is followed by a failure to connect
    /// during teardown, and the second exception buries the first — so a wrong password reports as
    /// "collection fixture threw in DisposeAsync" rather than as the actionable message below.
    /// Teardown must not try to drop a database that was never created.
    /// </remarks>
    private bool _created;

    public string ConnectionString { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        _adminConnectionString =
            Environment.GetEnvironmentVariable(EnvironmentVariable) ?? DefaultAdminConnectionString;

        _databaseName = $"kaff_test_{Guid.CreateVersion7():N}";

        try
        {
            await using var connection = new NpgsqlConnection(_adminConnectionString);
            await connection.OpenAsync();

            // The database name is generated here, never supplied by a caller, and identifiers cannot
            // be parameterised in PostgreSQL DDL.
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", connection);
            await command.ExecuteNonQueryAsync();

            _created = true;
        }
        catch (NpgsqlException exception)
        {
            throw new InvalidOperationException(
                $"Could not reach PostgreSQL for the test run ({exception.Message}). Set the "
                + $"{EnvironmentVariable} environment variable to a connection string for a "
                + "PostgreSQL 15 or later server whose user may CREATE DATABASE, for example:\n"
                + $"  $env:{EnvironmentVariable} = \"Host=localhost;Port=5432;Database=kaff;"
                + "Username=kaff;Password=kaff\"\n"
                + "That is what docker-compose.yml creates. Start it with: docker compose up -d db\n"
                + "These tests deliberately do not fall back to an in-memory provider: the rules they "
                + "check — append-only postings, the non-negative balance guard, the ledger "
                + "prohibitions — live in the database, and a provider that does not run them would "
                + "report safety that does not exist.",
                exception);
        }

        var builder = new NpgsqlConnectionStringBuilder(_adminConnectionString) { Database = _databaseName };
        ConnectionString = builder.ConnectionString;

        await using KaffDbContext context = CreateContext();
        var initializer = new DatabaseInitializer(context, NullLogger<DatabaseInitializer>.Instance);
        await initializer.InitialiseAsync(SchemaStrategy.CreateFromModel);
    }

    public async ValueTask DisposeAsync()
    {
        if (!_created)
        {
            return;
        }

        NpgsqlConnection.ClearAllPools();

        await using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            $"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)",
            connection);

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// A context wired the way the application wires it: append-only guard first, then audit.
    /// </summary>
    public KaffDbContext CreateContext(ICurrentUser? currentUser = null, IAuditContext? auditContext = null)
    {
        ICurrentUser user = currentUser ?? new SystemCurrentUser();
        IAuditContext audit = auditContext ?? new AuditContext();

        DbContextOptions<KaffDbContext> options = new DbContextOptionsBuilder<KaffDbContext>()
            .UseNpgsql(ConnectionString)
            .AddInterceptors(
                new AppendOnlySaveChangesInterceptor(),
                new AuditSaveChangesInterceptor(user, audit, TimeProvider.System))
            .EnableSensitiveDataLogging()
            .Options;

        return new KaffDbContext(options);
    }

    /// <summary>A context with no interceptors, for tests that need to observe raw behaviour.</summary>
    public KaffDbContext CreateBareContext()
    {
        DbContextOptions<KaffDbContext> options = new DbContextOptionsBuilder<KaffDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new KaffDbContext(options);
    }
}

/// <summary>Shares one database across every test class in the collection.</summary>
[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<PostgresDatabase>
{
    public const string Name = "postgres";
}
