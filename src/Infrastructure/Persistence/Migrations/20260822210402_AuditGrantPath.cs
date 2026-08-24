using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kaff.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuditGrantPath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "grant_path",
                table: "audit_records",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_records_grant_path",
                table: "audit_records",
                sql: "grant_path IS NULL OR (project_id IS NOT NULL AND grant_path <> 'None')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_records_grant_path",
                table: "audit_records");

            migrationBuilder.DropColumn(
                name: "grant_path",
                table: "audit_records");
        }
    }
}
