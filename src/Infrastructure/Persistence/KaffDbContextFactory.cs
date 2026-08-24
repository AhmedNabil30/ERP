using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kaff.Infrastructure.Persistence;

/// <summary>
/// Builds a context for <c>dotnet ef</c> without starting the Api.
/// </summary>
/// <remarks>
/// <para>
/// Without this, the EF tools reach into the Api's <c>Program</c> to find a service provider, which
/// drags in configuration they have no business needing — a connection string, a JWT signing key, an
/// environment name — and makes migration generation fail for reasons unrelated to the model.
/// </para>
/// <para>
/// <b>The connection string below is never opened.</b> Emitting a migration needs the model and the
/// provider, not a reachable database. Commands that genuinely do need to connect — <c>database
/// update</c>, <c>dbcontext script</c> — take a real one from <c>KAFF_DESIGN_TIME_DB</c>.
/// </para>
/// <para>
/// No interceptors are registered here. Design-time tooling writes no audit records because it
/// changes no business state.
/// </para>
/// </remarks>
public sealed class KaffDbContextFactory : IDesignTimeDbContextFactory<KaffDbContext>
{
    public const string ConnectionStringVariable = "KAFF_DESIGN_TIME_DB";

    private const string UnusedPlaceholder =
        "Host=localhost;Port=5432;Database=kaff_design_time;Username=kaff;Password=kaff";

    public KaffDbContext CreateDbContext(string[] args)
    {
        string connectionString =
            Environment.GetEnvironmentVariable(ConnectionStringVariable) ?? UnusedPlaceholder;

        DbContextOptions<KaffDbContext> options = new DbContextOptionsBuilder<KaffDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history"))
            .Options;

        return new KaffDbContext(options);
    }
}
