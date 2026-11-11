using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kaff.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BoqBases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "boq_bases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    archived_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_boq_bases", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "boq_base_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    boq_base_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    catalogue_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_boq_base_lines", x => x.id);
                    table.CheckConstraint("ck_boq_base_lines_quantity_not_negative", "quantity >= 0");
                    table.ForeignKey(
                        name: "FK_boq_base_lines_boq_bases_boq_base_id",
                        column: x => x.boq_base_id,
                        principalTable: "boq_bases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_boq_base_lines_catalogue_items_catalogue_item_id",
                        column: x => x.catalogue_item_id,
                        principalTable: "catalogue_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_boq_base_lines_base_position",
                table: "boq_base_lines",
                columns: new[] { "boq_base_id", "position" });

            migrationBuilder.CreateIndex(
                name: "IX_boq_base_lines_catalogue_item_id",
                table: "boq_base_lines",
                column: "catalogue_item_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "boq_base_lines");

            migrationBuilder.DropTable(
                name: "boq_bases");
        }
    }
}
