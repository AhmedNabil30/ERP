using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kaff.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Q70PhoneIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_suppliers_phone",
                table: "suppliers");

            migrationBuilder.DropIndex(
                name: "ux_subcontractors_phone",
                table: "subcontractors");

            migrationBuilder.DropIndex(
                name: "ux_employees_phone",
                table: "employees");

            migrationBuilder.CreateIndex(
                name: "ix_suppliers_phone",
                table: "suppliers",
                column: "phone_normalised");

            migrationBuilder.CreateIndex(
                name: "ix_subcontractors_phone",
                table: "subcontractors",
                column: "phone_normalised");

            migrationBuilder.CreateIndex(
                name: "ix_employees_phone",
                table: "employees",
                column: "phone_normalised");

            migrationBuilder.CreateIndex(
                name: "ux_employees_salaried_phone",
                table: "employees",
                column: "phone_normalised",
                unique: true,
                filter: "kind = 'Salaried'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_suppliers_phone",
                table: "suppliers");

            migrationBuilder.DropIndex(
                name: "ix_subcontractors_phone",
                table: "subcontractors");

            migrationBuilder.DropIndex(
                name: "ix_employees_phone",
                table: "employees");

            migrationBuilder.DropIndex(
                name: "ux_employees_salaried_phone",
                table: "employees");

            migrationBuilder.CreateIndex(
                name: "ux_suppliers_phone",
                table: "suppliers",
                column: "phone_normalised",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_subcontractors_phone",
                table: "subcontractors",
                column: "phone_normalised",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_employees_phone",
                table: "employees",
                column: "phone_normalised",
                unique: true);
        }
    }
}
