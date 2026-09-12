using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kaff.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Q83ActiveSalariedPhoneIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_employees_salaried_phone",
                table: "employees");

            migrationBuilder.CreateIndex(
                name: "ux_employees_salaried_phone",
                table: "employees",
                column: "phone_normalised",
                unique: true,
                filter: "kind = 'Salaried' AND is_active");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_employees_salaried_phone",
                table: "employees");

            migrationBuilder.CreateIndex(
                name: "ux_employees_salaried_phone",
                table: "employees",
                column: "phone_normalised",
                unique: true,
                filter: "kind = 'Salaried'");
        }
    }
}
