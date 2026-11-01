using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kaff.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExtractChain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "extracts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    period_end = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cumulative_certified_work = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    previously_certified_work = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    material_value_on_site = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    material_installed = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    approved_portion = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    held_for_ncr_id = table.Column<Guid>(type: "uuid", nullable: true),
                    held_portion_released_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    certified_value = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    net_payable = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_extracts", x => x.id);
                    table.ForeignKey(
                        name: "FK_extracts_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_extracts_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "extract_figures",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    extract_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    sign = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    label_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_extract_figures", x => x.id);
                    table.ForeignKey(
                        name: "FK_extract_figures_extracts_extract_id",
                        column: x => x.extract_id,
                        principalTable: "extracts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "extract_gate_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    extract_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gate = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    outcome = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    approved_portion = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_extract_gate_records", x => x.id);
                    table.ForeignKey(
                        name: "FK_extract_gate_records_extracts_extract_id",
                        column: x => x.extract_id,
                        principalTable: "extracts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_extract_gate_records_users_actor_user_id",
                        column: x => x.actor_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_extract_figures_extract_id",
                table: "extract_figures",
                column: "extract_id");

            migrationBuilder.CreateIndex(
                name: "IX_extract_gate_records_actor_user_id",
                table: "extract_gate_records",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_extract_gate_records_extract_id",
                table: "extract_gate_records",
                column: "extract_id");

            migrationBuilder.CreateIndex(
                name: "IX_extracts_created_by_user_id",
                table: "extracts",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_extracts_project_number",
                table: "extracts",
                columns: new[] { "project_id", "number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "extract_figures");

            migrationBuilder.DropTable(
                name: "extract_gate_records");

            migrationBuilder.DropTable(
                name: "extracts");
        }
    }
}
