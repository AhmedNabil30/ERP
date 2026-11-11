using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kaff.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DailyLogCorrections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "corrected_by_user_id",
                table: "executed_quantity_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "corrects_entry_id",
                table: "executed_quantity_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "corrected_by_user_id",
                table: "day_labour_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "corrects_entry_id",
                table: "day_labour_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_executed_quantity_entries_corrects_entry_id",
                table: "executed_quantity_entries",
                column: "corrects_entry_id");

            migrationBuilder.CreateIndex(
                name: "IX_day_labour_entries_corrects_entry_id",
                table: "day_labour_entries",
                column: "corrects_entry_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_executed_quantity_entries_corrects_entry_id",
                table: "executed_quantity_entries");

            migrationBuilder.DropIndex(
                name: "IX_day_labour_entries_corrects_entry_id",
                table: "day_labour_entries");

            migrationBuilder.DropColumn(
                name: "corrected_by_user_id",
                table: "executed_quantity_entries");

            migrationBuilder.DropColumn(
                name: "corrects_entry_id",
                table: "executed_quantity_entries");

            migrationBuilder.DropColumn(
                name: "corrected_by_user_id",
                table: "day_labour_entries");

            migrationBuilder.DropColumn(
                name: "corrects_entry_id",
                table: "day_labour_entries");
        }
    }
}
