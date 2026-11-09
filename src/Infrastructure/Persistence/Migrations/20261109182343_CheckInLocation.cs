using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kaff.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CheckInLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "site_latitude",
                table: "projects",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "site_longitude",
                table: "projects",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "accuracy_metres",
                table: "field_entries",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "flag_reason",
                table: "field_entries",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "latitude",
                table: "field_entries",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "longitude",
                table: "field_entries",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "site_latitude",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "site_longitude",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "accuracy_metres",
                table: "field_entries");

            migrationBuilder.DropColumn(
                name: "flag_reason",
                table: "field_entries");

            migrationBuilder.DropColumn(
                name: "latitude",
                table: "field_entries");

            migrationBuilder.DropColumn(
                name: "longitude",
                table: "field_entries");
        }
    }
}
