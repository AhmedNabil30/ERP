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

    /// <summary>
    /// Every check constraint this repository has decided must exist, written out rather than derived.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This list exists because the model-derived one cannot fail in the direction that matters.</b>
    /// D-064 read the required names from the EF model, on the reasoning that a hand-written list is
    /// one somebody forgets to extend. It is — but a derived list is one that agrees with whatever the
    /// model says today, including after somebody deletes a rule from it. The Verifier removed
    /// <c>ck_users_subcontractor_cannot_log_in</c> from <c>IdentityConfigurations</c> and every suite
    /// stayed green, <c>/api/health</c> went on reporting <c>guardsInstalled</c>, and the D-033
    /// start-up refusal could not fire for a guard the model no longer declared
    /// (qa/slice-1/verification-2026-08-27.md, <c>V-27-A</c>).
    /// </para>
    /// <para>
    /// The triggers above were never derived and are the half that worked: removing one stops the host
    /// booting. This gives the check constraints the same shape, and the forget-to-extend risk D-064
    /// named is answered by <c>SchemaInvariantTests</c>, which fails when this list and the model
    /// disagree either way. Adding or removing a constraint is therefore a deliberate two-file act —
    /// which is the property, not the friction.
    /// </para>
    /// <para>
    /// Ordered by the configuration file the constraint is declared in, so a reader can find it.
    /// </para>
    /// </remarks>
    public static readonly IReadOnlyList<string> RequiredCheckConstraints =
    [
        // AuditConfiguration
        "ck_audit_records_actor_is_named_completely",
        "ck_audit_records_entity_change_has_subject",
        "ck_audit_records_event_shape",
        "ck_audit_records_grant_path",
        "ck_audit_records_has_state",

        // IdentityConfigurations
        "ck_users_client_scope",
        "ck_users_operations_sub_department",
        "ck_users_subcontractor_cannot_log_in",
        "ck_project_assignments_revocation_complete",

        // MasterDataConfigurations
        "ck_babs_not_own_parent",
        "ck_catalogue_items_cost_not_negative",
        "ck_catalogue_items_rate_not_negative",
        "ck_employees_day_labour_has_trade",

        // ProjectConfigurations
        "ck_opportunities_closed_lost_reason",
        "ck_projects_area_positive",
        "ck_projects_cost_plus_terms",
        "ck_projects_design_terms",
        "ck_projects_link_complete",
        "ck_projects_lump_sum_terms",
        "ck_projects_not_linked_to_itself",
        "ck_projects_stoppage_reason",
        "ck_projects_termination_reason",

        // TreasuryConfigurations — the money rules of spec.md §6.1
        "ck_accounting_periods_month",
        "ck_accounting_periods_range",
        "ck_accounts_closed_after_opened",
        "ck_accounts_ledger_is_postable",
        "ck_accounts_party_complete",
        "ck_postings_amount_positive",
        "ck_postings_distinct_accounts",
        "ck_postings_not_self_reversing",
    ];

    /// <summary>
    /// Every check constraint the EF model declares. The other half of
    /// <see cref="RequiredCheckConstraints"/>, and what makes the two comparable.
    /// </summary>
    public static IReadOnlyList<string> ModelCheckConstraints(IModel designTimeModel)
    {
        ArgumentNullException.ThrowIfNull(designTimeModel);

        return [.. designTimeModel.GetEntityTypes()
            .SelectMany(entityType => entityType.GetCheckConstraints())
            .Select(constraint => constraint.Name)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];
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
    /// <para>
    /// Check constraints are verified from <b>two</b> lists, and the difference between them is the
    /// whole point. The EF model says what the schema of today declares; <see cref="RequiredCheckConstraints"/>
    /// says what this repository has decided must exist. See decisions.md D-064 and D-093.
    /// </para>
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

        // The check constraints, required from two independent lists.
        //
        // The model half (D-064) catches a database that drifted from the schema: a restore from a
        // schema-only dump, a migration applied by hand. It cannot catch a MODEL that lost a rule,
        // because deleting HasCheckConstraint deletes the expectation in the same edit —
        // qa/slice-1/verification-2026-08-27.md V-27-A, where dropping
        // ck_users_subcontractor_cannot_log_in left 97/97 and 215/215 green and guardsInstalled empty.
        //
        // RequiredCheckConstraints is the other half, hand-written for exactly the reason
        // requiredTriggers above is: a list nobody can edit by accident while editing the thing it
        // guards. D-064 rejected a hand-written list because it is one somebody forgets to extend —
        // true, and answered by SchemaInvariantTests, which fails when the two lists disagree in
        // either direction. See decisions.md D-093.
        //
        // The design-time model, not _context.Model: the run-time model is read-optimised and drops
        // check constraints entirely, so GetCheckConstraints() throws on it. Both are cached
        // singletons, so this is not rebuilt per call.
        IModel designTimeModel = _context.GetService<IDesignTimeModel>().Model;

        IEnumerable<string> requiredCheckConstraints = ModelCheckConstraints(designTimeModel)
            .Union(RequiredCheckConstraints, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

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
