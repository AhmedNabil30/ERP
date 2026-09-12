using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kaff.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SupplierCodeSequenceAndWithholdingRemoved : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "withholding_category",
                table: "suppliers");

            migrationBuilder.CreateSequence(
                name: "supplier_code_seq",
                startValue: 10001L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropSequence(
                name: "supplier_code_seq");

            migrationBuilder.AddColumn<string>(
                name: "withholding_category",
                table: "suppliers",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");
        }
    }
}
