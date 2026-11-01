using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kaff.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BoqAndRulings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "boqs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    frozen_on = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_boqs", x => x.id);
                    table.ForeignKey(
                        name: "FK_boqs_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "boq_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    boq_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    item_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    description_ar = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    description_en = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    unit = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    bab_code = table.Column<string>(type: "text", nullable: false),
                    bab_name_ar = table.Column<string>(type: "text", nullable: false),
                    bab_name_en = table.Column<string>(type: "text", nullable: false),
                    cost_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    base_sell_rate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    conditions_rate = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    line_markup = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    sell_rate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    quantity_estimated = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    quantity_surveyed = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    quantity_executed = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    is_custom = table.Column<bool>(type: "boolean", nullable: false),
                    pending_catalogue_review = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_boq_lines", x => x.id);
                    table.CheckConstraint("ck_boq_lines_prices_not_negative", "cost_price >= 0 AND base_sell_rate >= 0 AND sell_rate >= 0");
                    table.CheckConstraint("ck_boq_lines_quantities_not_negative", "(quantity_estimated IS NULL OR quantity_estimated >= 0) AND (quantity_surveyed IS NULL OR quantity_surveyed >= 0) AND quantity_executed >= 0");
                    table.CheckConstraint("ck_boq_lines_review_only_custom", "NOT pending_catalogue_review OR is_custom");
                    table.ForeignKey(
                        name: "FK_boq_lines_boqs_boq_id",
                        column: x => x.boq_id,
                        principalTable: "boqs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_boq_lines_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_projects_signed_has_value",
                table: "projects",
                sql: "contract_type = 'CostPlus' OR signed_on IS NULL OR contract_value IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_boq_lines_boq_position",
                table: "boq_lines",
                columns: new[] { "boq_id", "position" });

            migrationBuilder.CreateIndex(
                name: "IX_boq_lines_project_id",
                table: "boq_lines",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ux_boqs_project",
                table: "boqs",
                column: "project_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "boq_lines");

            migrationBuilder.DropTable(
                name: "boqs");

            migrationBuilder.DropCheckConstraint(
                name: "ck_projects_signed_has_value",
                table: "projects");
        }
    }
}
