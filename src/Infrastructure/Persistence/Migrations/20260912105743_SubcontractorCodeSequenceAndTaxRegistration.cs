using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kaff.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SubcontractorCodeSequenceAndTaxRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "withholding_category",
                table: "subcontractors");

            migrationBuilder.CreateSequence(
                name: "subcontractor_code_seq",
                startValue: 10001L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropSequence(
                name: "subcontractor_code_seq");

            migrationBuilder.AddColumn<string>(
                name: "withholding_category",
                table: "subcontractors",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");
        }
    }
}
