using Kaff.Domain.Auditing;
using Kaff.Domain.Authorization;
using Kaff.Domain.Contracts;
using Kaff.Domain.Contracts.Billing;
using Kaff.Domain.Contracts.Progress;
using Kaff.Domain.Identity;
using Kaff.Domain.Rulings;
using Kaff.Infrastructure.Auditing;
using Kaff.Infrastructure.Authorization;
using Kaff.Infrastructure.Persistence;
using Kaff.Infrastructure.Persistence.Interceptors;
using Kaff.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaff.Infrastructure;

/// <summary>Wires the infrastructure. Called once from the Api's <c>Program</c>.</summary>
public static class DependencyInjection
{
    /// <summary>Configuration key for the PostgreSQL connection string.</summary>
    public const string ConnectionStringName = "KaffDatabase";

    /// <summary>
    /// Configuration key for the owner's connection string: migrations, the guard scripts and the seeders, at
    /// start-up only. The request path never uses it (qa/slice-3/verification-2026-11-18.md F-1).
    /// </summary>
    public const string OwnerConnectionStringName = "KaffDatabaseOwner";

    public static IServiceCollection AddKaffInfrastructure(
        this IServiceCollection services,
        string connectionString,
        string? ownerConnectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        // Two roles, never one. Falling back to the application's connection for migrations would put the owner's
        // rights on the request path again, which is the defect this key exists to close.
        if (string.IsNullOrWhiteSpace(ownerConnectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{OwnerConnectionStringName}' is not configured. Migrations and the guard scripts "
                + $"run as the schema owner, and '{ConnectionStringName}' must be the least-privilege application role. "
                + "See deploy/README.md.");
        }

        services.TryAddSingleton(TimeProvider.System);

        // Every business rule the project has not had ruled lives in RulingSet. TryAdd so a test can
        // register a set with a different ruling.
        services.TryAddSingleton(RulingSet.Default);

        // The Api registers an HTTP-backed ICurrentUser before calling this, so TryAdd leaves it
        // alone. The system actor covers migrations, seeding and scheduled work, so that a change
        // made outside a request still records who made it.
        services.TryAddScoped<ICurrentUser, SystemCurrentUser>();

        services.AddScoped<AuditContext>();
        services.TryAddScoped<IAuditContext>(provider => provider.GetRequiredService<AuditContext>());

        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddScoped<AppendOnlySaveChangesInterceptor>();

        services.AddScoped<OwnerConnection>();

        services.AddDbContext<KaffDbContext>((provider, options) =>
        {
            string selected = provider.GetRequiredService<OwnerConnection>().InUse ? ownerConnectionString : connectionString;

            options.UseNpgsql(selected, npgsql =>
                npgsql.MigrationsHistoryTable("__ef_migrations_history"));

            // Order matters: refuse forbidden mutations before spending effort describing them.
            options.AddInterceptors(
                provider.GetRequiredService<AppendOnlySaveChangesInterceptor>(),
                provider.GetRequiredService<AuditSaveChangesInterceptor>());
        });

        // Both halves of "permission = role × assignment". The subject reader is the role half and
        // runs on every authorized request, including company-wide ones — see decisions.md D-048.
        services.AddScoped<IPermissionSubjectReader, PermissionSubjectReader>();
        services.AddScoped<IProjectAccessPolicy, ProjectAccessPolicy>();

        services.AddScoped<DatabaseInitializer>();
        services.AddScoped<AccountTreeSeeder>();
        services.AddScoped<BabSeeder>();

        services.AddKaffContractTypes();

        return services;
    }

    /// <summary>
    /// Registers the three billing calculators and the three progress metrics, and the dispatcher
    /// that resolves them.
    /// </summary>
    /// <remarks>
    /// CLAUDE.md: "Type dispatches, it does not fork." This is the whole of the dispatch wiring —
    /// three calculators, three metrics, one resolver. If a fourth contract type ever appears it
    /// appears here and nowhere else. Duplicate registrations for one type throw at construction
    /// rather than picking a winner silently.
    ///
    /// Registered separately from the database so tests can exercise dispatch without a connection.
    /// </remarks>
    public static IServiceCollection AddKaffContractTypes(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IBillingCalculator, LumpSumBillingCalculator>();
        services.AddScoped<IBillingCalculator, CostPlusBillingCalculator>();
        services.AddScoped<IBillingCalculator, DesignBillingCalculator>();

        services.AddScoped<IProgressMetric, LumpSumProgressMetric>();
        services.AddScoped<IProgressMetric, CostPlusProgressMetric>();
        services.AddScoped<IProgressMetric, DesignProgressMetric>();

        services.AddScoped<IContractTypeDispatcher>(provider => new ContractTypeDispatcher(
            provider.GetServices<IBillingCalculator>(),
            provider.GetServices<IProgressMetric>()));

        return services;
    }
}
