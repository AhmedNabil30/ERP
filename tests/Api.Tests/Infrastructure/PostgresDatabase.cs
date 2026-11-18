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
    /// <summary>
    /// Serialises schema and guard set-up across every database fixture in the process. The guard script revokes
    /// privileges from the cluster-wide role <c>kaff_app</c>; two databases doing that at once fail with
    /// <c>XX000: tuple concurrently updated</c>, which took out <c>CreateOwnerTests</c>'s class fixture whenever it
    /// initialised alongside the collection fixture.
    /// </summary>
    private static readonly SemaphoreSlim InitialisationGate = new(1, 1);

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

    /// <summary>
    /// The application's role, <c>kaff_app</c>: what <see cref="KaffApiFactory"/> serves requests as, and what a test
    /// reaching the database directly gets by default. It owns nothing and is no superuser, so a test that passes on
    /// it passes under the privilege model production runs (qa/slice-3/verification-2026-11-18.md F-1).
    /// </summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>
    /// The same database as the cluster administrator, which owns the schema it creates. For the start-up path, and
    /// for a test that must act as the owner: planting a row the application could never write, or proving a
    /// trigger holds even for the owner.
    /// </summary>
    public string AdminConnectionString { get; private set; } = string.Empty;

    /// <summary>The application role's name. Cluster-wide, so every fixture database shares it.</summary>
    public const string ApplicationRole = "kaff_app";

    /// <summary>Matches scripts/db/dev-app-role.sql, so the suite and the dev API share one role on a dev cluster.</summary>
    private const string ApplicationRolePassword = "kaff_app";

    /// <summary>
    /// Creates <see cref="ApplicationRole"/> on the cluster if it is absent, as scripts/db/dev-app-role.sql does.
    /// </summary>
    /// <remarks>
    /// Never alters an existing role: on a developer cluster it is the dev API's role too. The grants are not made
    /// here: <c>005_app_role.sql</c> makes them when the schema is initialised, exactly as at start-up.
    /// </remarks>
    public static async Task EnsureApplicationRoleAsync(string adminConnectionString)
    {
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            $"""
             DO $$
             BEGIN
                 IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = '{ApplicationRole}') THEN
                     CREATE ROLE {ApplicationRole} LOGIN PASSWORD '{ApplicationRolePassword}';
                 END IF;
             EXCEPTION WHEN duplicate_object OR unique_violation THEN NULL;
             END;
             $$
             """,
            connection);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary><paramref name="adminConnectionString"/>'s server and database, as <see cref="ApplicationRole"/>.</summary>
    public static string ApplicationConnectionString(string adminConnectionString) =>
        new NpgsqlConnectionStringBuilder(adminConnectionString)
        {
            Username = ApplicationRole,
            Password = ApplicationRolePassword,
        }.ConnectionString;

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
        AdminConnectionString = builder.ConnectionString;
        ConnectionString = ApplicationConnectionString(AdminConnectionString);

        await using KaffDbContext context = CreateAdminContext();
        var initializer = new DatabaseInitializer(context, NullLogger<DatabaseInitializer>.Instance);
        await InitialisationGate.WaitAsync();
        try
        {
            await EnsureApplicationRoleAsync(_adminConnectionString);
            await initializer.InitialiseAsync(SchemaStrategy.CreateFromModel);
        }
        finally
        {
            InitialisationGate.Release();
        }
    }

    /// <summary>
    /// Drops this fixture's database.
    /// </summary>
    /// <remarks>
    /// This runs on a normal finish, including a failing run. It does not run when the test process is
    /// killed outright — a session limit, a rate limit, a cancelled run — and each killed run then leaves
    /// one database of roughly 10 MB behind. Twenty-three had accumulated by 2026-11-06, 272 MB in all.
    /// <para>
    /// There is deliberately no sweeper here. A sweeper cannot tell a database abandoned by a dead process
    /// from one a sibling fixture created moments ago and has not yet connected to, so it would drop live
    /// databases to tidy dead ones. Clean them by hand when they accumulate, with no test run in progress:
    /// </para>
    /// <code>
    /// select datname from pg_database where datname like 'kaff\_test\_%';
    /// </code>
    /// </remarks>
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
        => CreateContext(ConnectionString, currentUser, auditContext);

    /// <summary>
    /// <see cref="CreateContext(ICurrentUser?, IAuditContext?)"/> as the owner. Only for what the application's role
    /// must not be able to do; see <see cref="AdminConnectionString"/>.
    /// </summary>
    public KaffDbContext CreateAdminContext(ICurrentUser? currentUser = null, IAuditContext? auditContext = null)
        => CreateContext(AdminConnectionString, currentUser, auditContext);

    private static KaffDbContext CreateContext(string connectionString, ICurrentUser? currentUser, IAuditContext? auditContext)
    {
        ICurrentUser user = currentUser ?? new SystemCurrentUser();
        IAuditContext audit = auditContext ?? new AuditContext();

        DbContextOptions<KaffDbContext> options = new DbContextOptionsBuilder<KaffDbContext>()
            .UseNpgsql(connectionString)
            .AddInterceptors(
                new AppendOnlySaveChangesInterceptor(),
                new AuditSaveChangesInterceptor(user, audit, TimeProvider.System))
            .EnableSensitiveDataLogging()
            .Options;

        return new KaffDbContext(options);
    }

    /// <summary>A context with no interceptors, for tests that need to observe raw behaviour.</summary>
    public KaffDbContext CreateBareContext() => CreateBareContext(ConnectionString);

    /// <summary>
    /// <see cref="CreateBareContext()"/> as the owner: for a test proving a trigger binds even the owner, for test
    /// clean-up the application role may not do, and for planting a row only the owner could write.
    /// </summary>
    public KaffDbContext CreateBareAdminContext() => CreateBareContext(AdminConnectionString);

    private static KaffDbContext CreateBareContext(string connectionString) =>
        new(new DbContextOptionsBuilder<KaffDbContext>().UseNpgsql(connectionString).Options);
}

/// <summary>Shares one database across every test class in the collection.</summary>
[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<PostgresDatabase>
{
    public const string Name = "postgres";
}
