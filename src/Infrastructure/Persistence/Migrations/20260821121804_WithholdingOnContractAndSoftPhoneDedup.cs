using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kaff.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Karim's rulings of 2026-08-21 — decisions.md D-049.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Withholding moves from the client to the contract.</b> spec.md §6.7 sets the rate by what is
    /// supplied and §5.4 lets one client hold a design contract and an execution contract at once, so
    /// one value per client could never have expressed both.
    /// </para>
    /// <para>
    /// <b>The client phone index stops being unique.</b> "Allow duplicates, but show a soft
    /// warning … Do not block the save." The index remains so the lookup behind that warning stays
    /// fast.
    /// </para>
    /// <para>
    /// EF warned that this "may result in the loss of data", and it is right in principle: dropping
    /// <c>clients.withholding_category</c> discards whatever it held. No client rows exist yet, so
    /// nothing is lost here — but a deployment that already carried clients would need the values
    /// copied onto their projects first, and there is no automatic mapping for a client with two
    /// contracts, which is the whole reason for the move.
    /// </para>
    /// </remarks>
    public partial class WithholdingOnContractAndSoftPhoneDedup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_clients_phone",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "withholding_category",
                table: "clients");

            // defaultValue is "None", not the empty string EF scaffolded. The enum is stored as text
            // so the SQL guards can compare it by name, and "" is not a member of it — an existing
            // row backfilled with "" would fail to materialise on the next read, as a cast error
            // rather than as anything that names the cause.
            migrationBuilder.AddColumn<string>(
                name: "withholding_category",
                table: "projects",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.CreateIndex(
                name: "ix_clients_phone",
                table: "clients",
                column: "phone_normalised");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_clients_phone",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "withholding_category",
                table: "projects");

            // Same reasoning as Up: "None" is a member of the enum and "" is not.
            //
            // Note what Down cannot do: a project's rate cannot be pushed back onto its client when
            // two projects for one client disagree. Reversing this migration restores the shape, not
            // the data.
            migrationBuilder.AddColumn<string>(
                name: "withholding_category",
                table: "clients",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.CreateIndex(
                name: "ux_clients_phone",
                table: "clients",
                column: "phone_normalised",
                unique: true);
        }
    }
}
