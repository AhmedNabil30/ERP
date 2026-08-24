using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kaff.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Installs the database guards: the append-only triggers, the non-negative balance check, the
    /// ledger and hold rules, the account-immutability trigger, and the derived balances view.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This migration exists because a schema without the guards is not a usable database. Provision
    /// one with <c>dotnet ef database update</c> — or by handing a DBA the output of
    /// <c>dotnet ef migrations script</c>, which is how production is built — and before this
    /// migration existed you got tables with no append-only trigger, no safe floor and no ledger
    /// rules. The application installed them on its next boot, which leaves a window in which the
    /// database holds money and enforces nothing. spec.md §6.1 requires the safe rule to live in the
    /// database; it now arrives with the schema that needs it.
    /// </para>
    /// <para>
    /// The SQL is read from the embedded scripts rather than pasted here, so there is one source of
    /// truth. <c>dotnet ef migrations script</c> executes <c>Up</c> to collect operations, so the
    /// generated script contains the full text.
    /// </para>
    /// <para>
    /// The scripts are idempotent — <c>CREATE OR REPLACE FUNCTION</c>, <c>DROP TRIGGER IF EXISTS</c>
    /// — which is what makes it safe for <see cref="DatabaseInitializer"/> to apply them again at
    /// start-up. That belt-and-braces is deliberate: it also covers the test harness, which builds
    /// its schema from the model rather than from migrations.
    /// </para>
    /// <para>
    /// <b>Editing an applied guard script changes what this migration emits for a fresh database.</b>
    /// Because the scripts are idempotent and re-applied on every boot, both paths converge — but a
    /// change to a guard is a change to a money rule, and belongs in <c>decisions.md</c> either way.
    /// </para>
    /// </remarks>
    public partial class DatabaseGuards : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (string sql in GuardScripts.ReadAllInOrder())
            {
                migrationBuilder.Sql(sql);
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Dropping the guards would leave a schema that still holds postings but no longer
            // refuses to mutate them. There is no safe way down from here: reverting past this
            // migration means dropping the tables too, which the Initial migration's Down does.
            migrationBuilder.Sql("DROP VIEW IF EXISTS account_balances;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_postings_append_only ON postings;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_postings_no_truncate ON postings;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_audit_records_append_only ON audit_records;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_audit_records_no_truncate ON audit_records;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_postings_validate ON postings;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_postings_non_negative_balance ON postings;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_postings_hold_release_in_full ON postings;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_accounts_configuration_immutable ON accounts;");
        }
    }
}
