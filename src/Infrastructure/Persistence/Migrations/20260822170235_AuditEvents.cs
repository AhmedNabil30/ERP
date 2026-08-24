using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kaff.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuditEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_records_has_state",
                table: "audit_records");

            migrationBuilder.AddColumn<string>(
                name: "event_type",
                table: "audit_records",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_records_event_shape",
                table: "audit_records",
                sql: "(action = 'Occurred') = (event_type IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_records_has_state",
                table: "audit_records",
                sql: "event_type IS NOT NULL OR before_json IS NOT NULL OR after_json IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_records_event_shape",
                table: "audit_records");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_records_has_state",
                table: "audit_records");

            migrationBuilder.DropColumn(
                name: "event_type",
                table: "audit_records");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_records_has_state",
                table: "audit_records",
                sql: "before_json IS NOT NULL OR after_json IS NOT NULL");
        }
    }
}
