using System.Reflection;
using Kaff.Domain.Treasury;
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

    /// <summary>
    /// PostgreSQL's own re-printed definition of every required check constraint — a snapshot taken
    /// the day the predicate was last reviewed, not the C# string that authored it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What this closes.</b> qa/slice-1/verification-2026-08-30.md's <c>V-30-D</c>: every check
    /// above this one verifies a check constraint by <i>name</i>. <c>MUT-C3</c> kept
    /// <c>ck_users_subcontractor_cannot_log_in</c>'s name and replaced its predicate with
    /// <c>1 = 1</c> — build clean, Api suite 227/227, D-033's refusal silent, because nothing anywhere
    /// read the expression. D-093 named this gap in its own prose: *"a migration that keeps
    /// <c>ck_postings_amount_positive</c> and changes its predicate to <c>amount &gt;= 0</c> passes
    /// every check here."* This dictionary is what makes that migration fail instead.
    /// </para>
    /// <para>
    /// <b>Why PostgreSQL's re-print, and not the authored SQL in <c>Persistence/Configurations</c>.</b>
    /// Measured on PostgreSQL 16 (decisions.md D-101 §5): the authored <c>amount &gt; 0</c> and the
    /// live <c>pg_get_constraintdef</c> answer of <c>CHECK ((amount &gt; (0)::numeric))</c> never
    /// match — PostgreSQL adds parentheses and explicit casts to every predicate it stores, so
    /// comparing the authored text would report all thirty as failing on a correct database the day it
    /// shipped. PostgreSQL's own re-print, though, <i>is</i> a stable normal form: two constraints
    /// created with different but equivalent whitespace re-print identically, and a genuinely changed
    /// predicate re-prints differently. This dictionary snapshots that re-print, and
    /// <see cref="FindMissingGuardsAsync"/> compares the live re-print against it — never the C#
    /// string above it.
    /// </para>
    /// <para>
    /// <b>This has D-093's own property, for the same reason <see cref="RequiredCheckConstraints"/>
    /// does.</b> It lives in this file, not in <c>Persistence/Configurations</c>, so editing a
    /// predicate there cannot also update its own snapshot in the same keystroke. Changing a
    /// constraint's predicate — even under its unchanged name — is now a deliberate edit in two files,
    /// and <c>SchemaInvariantTests.Every_required_check_constraint_has_a_recorded_definition</c>
    /// (tests/Api.Tests) is what makes forgetting the second one loud.
    /// </para>
    /// <para>
    /// <b>One residual, named rather than solved (D-101 §5.3).</b> A semantically identical rewrite can
    /// re-print differently — <c>0 &lt; amount</c> re-prints as <c>CHECK (((0)::numeric &lt; amount))</c>,
    /// not as this dictionary's <c>amount &gt; 0</c> entry — and is flagged as a mismatch. That is
    /// D-093's two-file friction working as designed for a deliberate edit to a money guard: re-approve
    /// the snapshot in the same commit. It is not formatting noise to be normalised away.
    /// </para>
    /// </remarks>
    public static readonly IReadOnlyDictionary<string, string> RequiredCheckConstraintDefinitions =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // AuditConfiguration
            ["ck_audit_records_actor_is_named_completely"] =
                "CHECK (((actor_user_id IS NULL) = (actor_role IS NULL)))",
            ["ck_audit_records_entity_change_has_subject"] =
                "CHECK ((((action)::text = 'Occurred'::text) OR (entity_id IS NOT NULL)))",
            ["ck_audit_records_event_shape"] =
                "CHECK ((((action)::text = 'Occurred'::text) = (event_type IS NOT NULL)))",
            ["ck_audit_records_grant_path"] =
                "CHECK (((grant_path IS NULL) OR ((project_id IS NOT NULL) AND ((grant_path)::text <> 'None'::text))))",
            ["ck_audit_records_has_state"] =
                "CHECK (((event_type IS NOT NULL) OR (before_json IS NOT NULL) OR (after_json IS NOT NULL)))",

            // IdentityConfigurations
            ["ck_users_client_scope"] =
                "CHECK (((((role)::text = 'Client'::text) AND (client_id IS NOT NULL)) OR (((role)::text <> 'Client'::text) AND (client_id IS NULL))))",
            ["ck_users_operations_sub_department"] =
                "CHECK (((((department)::text = 'Operations'::text) AND (operations_sub_department IS NOT NULL)) OR (((department)::text IS DISTINCT FROM 'Operations'::text) AND (operations_sub_department IS NULL))))",
            ["ck_users_subcontractor_cannot_log_in"] =
                "CHECK ((((role)::text <> 'Subcontractor'::text) OR (password_hash IS NULL)))",
            ["ck_project_assignments_revocation_complete"] =
                "CHECK ((((revoked_at IS NULL) AND (revoked_by_user_id IS NULL)) OR ((revoked_at IS NOT NULL) AND (revoked_by_user_id IS NOT NULL))))",

            // MasterDataConfigurations
            ["ck_babs_not_own_parent"] =
                "CHECK (((parent_bab_id IS NULL) OR (parent_bab_id <> id)))",
            ["ck_catalogue_items_cost_not_negative"] =
                "CHECK ((cost_price >= (0)::numeric))",
            ["ck_catalogue_items_rate_not_negative"] =
                "CHECK ((base_sell_rate >= (0)::numeric))",
            ["ck_employees_day_labour_has_trade"] =
                "CHECK ((((kind)::text <> 'DayLabour'::text) OR (bab_id IS NOT NULL)))",

            // ProjectConfigurations
            ["ck_opportunities_closed_lost_reason"] =
                "CHECK ((((status)::text <> 'ClosedLost'::text) OR (closed_lost_reason IS NOT NULL)))",
            ["ck_projects_area_positive"] =
                "CHECK (((area_square_metres IS NULL) OR (area_square_metres > (0)::numeric)))",
            ["ck_projects_cost_plus_terms"] =
                "CHECK ((((contract_type)::text = 'CostPlus'::text) OR (supervision_rate IS NULL)))",
            ["ck_projects_design_terms"] =
                "CHECK ((((contract_type)::text = 'Design'::text) OR ((area_square_metres IS NULL) AND (design_rate_per_square_metre IS NULL))))",
            ["ck_projects_link_complete"] =
                "CHECK ((((linked_project_id IS NULL) AND (link_type IS NULL)) OR ((linked_project_id IS NOT NULL) AND (link_type IS NOT NULL))))",
            ["ck_projects_lump_sum_terms"] =
                "CHECK ((((contract_type)::text = 'LumpSum'::text) OR ((advance_rate IS NULL) AND (hold_rate IS NULL) AND (advance_recovery_rate IS NULL) AND (material_advance_rate IS NULL))))",
            ["ck_projects_not_linked_to_itself"] =
                "CHECK (((linked_project_id IS NULL) OR (linked_project_id <> id)))",
            ["ck_projects_stoppage_reason"] =
                "CHECK (((stopped_on IS NULL) OR (stoppage_reason IS NOT NULL)))",
            ["ck_projects_termination_reason"] =
                "CHECK (((terminated_on IS NULL) OR (termination_reason IS NOT NULL)))",

            // TreasuryConfigurations — the money rules of spec.md §6.1
            ["ck_accounting_periods_month"] =
                "CHECK (((month >= 1) AND (month <= 12)))",
            ["ck_accounting_periods_range"] =
                "CHECK ((ends_on >= starts_on))",
            ["ck_accounts_closed_after_opened"] =
                "CHECK (((closed_on IS NULL) OR (closed_on >= opened_on)))",
            ["ck_accounts_ledger_is_postable"] =
                "CHECK (((ledger_kind IS NULL) OR (is_postable = true)))",
            ["ck_accounts_party_complete"] =
                "CHECK ((((party_type IS NULL) AND (party_id IS NULL)) OR ((party_type IS NOT NULL) AND (party_id IS NOT NULL))))",
            ["ck_postings_amount_positive"] =
                "CHECK ((amount > (0)::numeric))",
            ["ck_postings_distinct_accounts"] =
                "CHECK ((from_account_id <> to_account_id))",
            ["ck_postings_not_self_reversing"] =
                "CHECK (((reverses_id IS NULL) OR (reverses_id <> id)))",
        };

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
    /// <para>
    /// <b>Two checks here are of data rather than of a name; every other one is of existence only</b> —
    /// a <c>tgname</c>, an <c>indexname</c>, a bare <c>conname</c>. The safe floor is one: which
    /// accounts are floored lives in <c>accounts.enforce_non_negative</c> rather than in the trigger,
    /// so <c>trg_postings_non_negative_balance</c> can be present, correct and running, and floor
    /// nothing (decisions.md D-101). A check constraint's <i>predicate</i> is the other, added for the
    /// same reason: <c>ck_postings_amount_positive</c> can be present under its required name with its
    /// predicate weakened to <c>amount &gt;= 0</c>, and a name-only check cannot tell
    /// (qa/slice-1/verification-2026-08-30.md <c>V-30-D</c>; see
    /// <see cref="RequiredCheckConstraintDefinitions"/>).
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
        // and /api/health calls this method on every poll. Both the name and PostgreSQL's own
        // re-printed definition come back together, so the definition comparison below costs nothing
        // beyond the name check that already had to run.
        List<CheckConstraintRow> presentCheckConstraintRows = await _context.Database
            .SqlQuery<CheckConstraintRow>(
                $"""
                 SELECT conname::text AS "Name", pg_get_constraintdef(oid) AS "Definition"
                 FROM pg_constraint WHERE contype = 'c'
                 """)
            .ToListAsync(cancellationToken);

        Dictionary<string, string> presentDefinitionsByName = presentCheckConstraintRows
            .ToDictionary(row => row.Name, row => row.Definition, StringComparer.Ordinal);

        missing.AddRange(requiredCheckConstraints.Except(presentDefinitionsByName.Keys, StringComparer.Ordinal));

        // V-30-D. A constraint present under its required name is not necessarily the constraint that
        // name was given to — MUT-C3 kept ck_users_subcontractor_cannot_log_in's name and replaced its
        // predicate with "1 = 1", and every check above this one is satisfied. Compared only for
        // constraints found present: an absent one is already reported by the Except() above, and
        // reporting it twice would obscure which defect actually occurred.
        foreach ((string name, string expectedDefinition) in RequiredCheckConstraintDefinitions)
        {
            if (presentDefinitionsByName.TryGetValue(name, out string? actualDefinition)
                && !string.Equals(actualDefinition, expectedDefinition, StringComparison.Ordinal))
            {
                missing.Add(
                    $"{name} predicate changed: expected \"{expectedDefinition}\", found \"{actualDefinition}\"");
            }
        }

        // The safe floor is DATA, not code, and every check above it except the predicate comparison
        // just above is a check of a NAME.
        //
        // kaff_check_non_negative_balance reads accounts.enforce_non_negative and floors only the rows
        // that carry it (001_guards.sql section 3). So trg_postings_non_negative_balance can be present
        // under its required name, run on every insert, and floor nothing at all — measured on
        // 2026-09-02: a Safe row INSERTed with the flag false took an overdraw to -4,000 while this
        // method returned an empty list. Nothing in either suite could see it, because every test
        // builds its accounts through Account.Create and therefore always from the current catalogue.
        //
        // The exposure is a real deployment, not a hypothetical: AccountTreeSeeder inserts SAFE-MAIN on
        // every start-up and never rewrites it, and 001_guards.sql's own section 3 warns that "a
        // database seeded before 2026-08-20 therefore keeps the old floors". trg_accounts_configuration
        // _immutable does NOT close this — it is BEFORE UPDATE, so it refuses to repair a wrong row
        // while permitting an INSERT to create one.
        //
        // Both directions are defects, which is why this compares rather than only looking for absence:
        // a floor missing lets an account overdraw (spec.md §6.1), and a floor added refuses a
        // legitimate posting with an opaque KAFF_NEGATIVE_BALANCE mid-extract. Domain.Tests
        // CatalogueCompletenessTests pins the same set in the catalogue; this pins the rows against it.
        string[] flooredTypes =
        [
            .. AccountTypes.All
                .Where(meta => meta.EnforceNonNegative)
                .Select(meta => meta.Type.ToString())
        ];

        List<string> misfloored = await _context.Database
            .SqlQuery<string>(
                $"""
                 SELECT code::text AS "Value" FROM accounts
                 WHERE enforce_non_negative <> (type = ANY({flooredTypes}))
                 ORDER BY code
                 """)
            .ToListAsync(cancellationToken);

        missing.AddRange(misfloored.Select(code => $"accounts.enforce_non_negative on {code}"));

        if (missing.Count > 0)
        {
            _logger.LogError("Database guards are missing: {Missing}.", string.Join(", ", missing));
        }

        return missing;
    }

    /// <summary>One row of <c>pg_constraint</c>, projected for <see cref="FindMissingGuardsAsync"/>.</summary>
    private sealed record CheckConstraintRow(string Name, string Definition);
}
