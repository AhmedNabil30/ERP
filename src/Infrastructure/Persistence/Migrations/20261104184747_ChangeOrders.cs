using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kaff.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ChangeOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "change_orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    is_priced = table.Column<bool>(type: "boolean", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_change_orders", x => x.id);
                    table.ForeignKey(
                        name: "FK_change_orders_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_change_orders_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "change_order_gate_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    change_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gate = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    outcome = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_change_order_gate_records", x => x.id);
                    table.ForeignKey(
                        name: "FK_change_order_gate_records_change_orders_change_order_id",
                        column: x => x.change_order_id,
                        principalTable: "change_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_change_order_gate_records_users_actor_user_id",
                        column: x => x.actor_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "change_order_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    change_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    unit = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_change_order_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_change_order_lines_change_orders_change_order_id",
                        column: x => x.change_order_id,
                        principalTable: "change_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "extract_change_order_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    extract_id = table.Column<Guid>(type: "uuid", nullable: false),
                    change_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_extract_change_order_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_extract_change_order_lines_change_orders_change_order_id",
                        column: x => x.change_order_id,
                        principalTable: "change_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_extract_change_order_lines_extracts_extract_id",
                        column: x => x.extract_id,
                        principalTable: "extracts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_change_order_gate_records_actor_user_id",
                table: "change_order_gate_records",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_change_order_gate_records_change_order_id",
                table: "change_order_gate_records",
                column: "change_order_id");

            migrationBuilder.CreateIndex(
                name: "IX_change_order_lines_change_order_id",
                table: "change_order_lines",
                column: "change_order_id");

            migrationBuilder.CreateIndex(
                name: "IX_change_orders_created_by_user_id",
                table: "change_orders",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_change_orders_project_number",
                table: "change_orders",
                columns: new[] { "project_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_extract_change_order_lines_extract_id",
                table: "extract_change_order_lines",
                column: "extract_id");

            migrationBuilder.CreateIndex(
                name: "ux_extract_change_order_lines_change_order",
                table: "extract_change_order_lines",
                column: "change_order_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "change_order_gate_records");

            migrationBuilder.DropTable(
                name: "change_order_lines");

            migrationBuilder.DropTable(
                name: "extract_change_order_lines");

            migrationBuilder.DropTable(
                name: "change_orders");
        }
    }
}
