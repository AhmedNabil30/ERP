using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kaff.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AccountingRequestSubjectKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "subject_key",
                table: "accounting_requests",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_accounting_requests_waiting_subject",
                table: "accounting_requests",
                columns: new[] { "kind", "subject_key" },
                unique: true,
                filter: "status = 'Requested' AND subject_key IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_accounting_requests_waiting_subject",
                table: "accounting_requests");

            migrationBuilder.DropColumn(
                name: "subject_key",
                table: "accounting_requests");
        }
    }
}
