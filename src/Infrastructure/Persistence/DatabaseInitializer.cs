using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;

namespace Kaff.Infrastructure.Persistence;

/// <summary>How the schema itself is created.</summary>
public enum SchemaStrategy
{
    /// <summary>Apply EF Core migrations. The production and staging path.</summary>
    Migrate = 1,

    /// <summary>
    /// Build the schema straight from the model. Used by the test harness, which creates and drops a
    /// database per run and has no interest in migration history.
    /// </summary>
    CreateFromModel = 2,
}

/// <summary>
/// Brings a database up to date: schema, then guards.
/// </summary>
/// <remarks>
/// <para>
/// The guard scripts are applied on every start-up, after the schema, by both strategies. They are
/// idempotent, and applying them unconditionally means a database can never be running with the
/// schema of today and the triggers of last month — which would leave the safe-never-negative rule
/// silently switched off.
/// </para>
/// <para>
/// Scripts run in file-name order, so the numeric prefixes matter.
/// </para>
/// </remarks>
public sealed class DatabaseInitializer
{
    private readonly KaffDbContext _context;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(KaffDbContext context, ILogger<DatabaseInitializer> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task InitialiseAsync(SchemaStrategy strategy, CancellationToken cancellationToken = default)
    {
        switch (strategy)
        {
            case SchemaStrategy.Migrate:
                _logger.LogInformation("Applying EF Core migrations.");
                await _context.Database.MigrateAsync(cancellationToken);
                break;

            case SchemaStrategy.CreateFromModel:
                _logger.LogInformation("Creating the schema from the model.");
                await _context.Database.EnsureCreatedAsync(cancellationToken);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(strategy), strategy, "Unknown schema strategy.");
        }

        await ApplyGuardsAsync(cancellationToken);
    }

    /// <summary>
    /// Applies every embedded <c>.sql</c> guard script. Safe to call repeatedly.
    /// </summary>
    public async Task ApplyGuardsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> scripts = GuardScripts.ReadAllInOrder();

        for (int index = 0; index < scripts.Count; index++)
        {
            _logger.LogInformation("Applying database guard script {Index} of {Count}.", index + 1, scripts.Count);

#pragma warning disable EF1002 // The SQL is an embedded asset of this assembly, not user input.
            await _context.Database.ExecuteSqlRawAsync(scripts[index], cancellationToken);
#pragma warning restore EF1002
        }
    }

    /// <summary>
    /// Confirms the guards are actually installed.
    /// </summary>
    /// <remarks>
    /// Called at start-up. A deployment that lost its triggers would still serve traffic and would
    /// still pass every application-level test, while the one rule spec.md §6.1 insists must live in
    /// the database is not there. Better to refuse to start.
    /// </remarks>
    /// <remarks>
    /// Check constraints are verified too, and the list of them is read from the EF model rather than
    /// written out here. A hand-maintained list of names is a list somebody forgets to extend — the
    /// same class of defect this method exists to catch. See decisions.md D-063 A-01.
    /// </remarks>
    public async Task<IReadOnlyList<string>> FindMissingGuardsAsync(CancellationToken cancellationToken = default)
    {
        // Every guard, not a representative sample. A database missing the reverses-uniqueness index
        // starts cleanly, reports healthy, and silently permits a correction to be counted twice.
        string[] requiredTriggers =
        [
            "trg_postings_append_only",
            "trg_postings_no_truncate",
            "trg_audit_records_append_only",
            "trg_audit_records_no_truncate",
            "trg_postings_validate",
            "trg_postings_non_negative_balance",
            "trg_postings_hold_release_in_full",
            "trg_accounts_configuration_immutable",
        ];

        string[] requiredIndexes =
        [
            "ux_postings_reverses",
            "ux_accounts_project_dimension",
            "ux_accounts_company_ledger",
        ];

        List<string> missing = [];

        foreach (string trigger in requiredTriggers)
        {
            int count = await _context.Database
                .SqlQuery<int>($"SELECT COUNT(*)::int AS \"Value\" FROM pg_trigger WHERE tgname::text = {trigger}")
                .SingleAsync(cancellationToken);

            if (count == 0)
            {
                missing.Add(trigger);
            }
        }

        foreach (string index in requiredIndexes)
        {
            int count = await _context.Database
                .SqlQuery<int>($"SELECT COUNT(*)::int AS \"Value\" FROM pg_indexes WHERE indexname::text = {index}")
                .SingleAsync(cancellationToken);

            if (count == 0)
            {
                missing.Add(index);
            }
        }

        const string balancesView = "account_balances";

        int viewCount = await _context.Database
            .SqlQuery<int>($"SELECT COUNT(*)::int AS \"Value\" FROM pg_views WHERE viewname::text = {balancesView}")
            .SingleAsync(cancellationToken);

        if (viewCount == 0)
        {
            missing.Add(balancesView);
        }

        // The check constraints. Every one of them is declared through HasCheckConstraint in
        // Persistence/Configurations, so the model is the complete list and cannot drift out of step
        // with the schema the migrations build from that same model.
        //
        // The design-time model, not _context.Model: the run-time model is read-optimised and drops
        // check constraints entirely, so GetCheckConstraints() throws on it. Both are cached
        // singletons, so this is not rebuilt per call.
        IModel designTimeModel = _context.GetService<IDesignTimeModel>().Model;

        string[] requiredCheckConstraints = designTimeModel.GetEntityTypes()
            .SelectMany(entityType => entityType.GetCheckConstraints())
            .Select(constraint => constraint.Name)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        // One query for all of them rather than one each, unlike the loops above: there are dozens,
        // and /api/health calls this method on every poll.
        List<string> presentCheckConstraints = await _context.Database
            .SqlQuery<string>($"SELECT conname::text AS \"Value\" FROM pg_constraint WHERE contype = 'c'")
            .ToListAsync(cancellationToken);

        missing.AddRange(requiredCheckConstraints.Except(presentCheckConstraints, StringComparer.Ordinal));

        if (missing.Count > 0)
        {
            _logger.LogError("Database guards are missing: {Missing}.", string.Join(", ", missing));
        }

        return missing;
    }
}
