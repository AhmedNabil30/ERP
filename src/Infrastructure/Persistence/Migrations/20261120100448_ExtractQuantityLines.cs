using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kaff.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExtractQuantityLines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "extract_quantity_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    extract_id = table.Column<Guid>(type: "uuid", nullable: false),
                    take = table.Column<int>(type: "integer", nullable: false),
                    boq_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    previous_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    cumulative_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_extract_quantity_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_extract_quantity_lines_extracts_extract_id",
                        column: x => x.extract_id,
                        principalTable: "extracts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_extract_quantity_lines_extract_take_line",
                table: "extract_quantity_lines",
                columns: new[] { "extract_id", "take", "boq_line_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "extract_quantity_lines");
        }
    }
}
