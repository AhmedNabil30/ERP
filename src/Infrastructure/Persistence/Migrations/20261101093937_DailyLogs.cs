using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kaff.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DailyLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "daily_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    engineer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    log_date = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_daily_logs_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_daily_logs_users_engineer_id",
                        column: x => x.engineer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "day_labour_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hours = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    daily_log_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_day_labour_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_day_labour_entries_daily_logs_daily_log_id",
                        column: x => x.daily_log_id,
                        principalTable: "daily_logs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_day_labour_entries_employees_employee_id",
                        column: x => x.employee_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_day_labour_entries_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "executed_quantity_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    boq_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    daily_log_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_executed_quantity_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_executed_quantity_entries_daily_logs_daily_log_id",
                        column: x => x.daily_log_id,
                        principalTable: "daily_logs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_executed_quantity_entries_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "field_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    field = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    daily_log_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_field_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_field_entries_daily_logs_daily_log_id",
                        column: x => x.daily_log_id,
                        principalTable: "daily_logs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_field_entries_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "material_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    material_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_stockpile = table.Column<bool>(type: "boolean", nullable: false),
                    daily_log_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_material_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_material_entries_daily_logs_daily_log_id",
                        column: x => x.daily_log_id,
                        principalTable: "daily_logs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_material_entries_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "photo_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_reference = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    caption = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    is_published = table.Column<bool>(type: "boolean", nullable: false),
                    daily_log_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_photo_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_photo_entries_daily_logs_daily_log_id",
                        column: x => x.daily_log_id,
                        principalTable: "daily_logs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_photo_entries_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "site_expense_drafts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    daily_log_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_site_expense_drafts", x => x.id);
                    table.ForeignKey(
                        name: "FK_site_expense_drafts_daily_logs_daily_log_id",
                        column: x => x.daily_log_id,
                        principalTable: "daily_logs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_site_expense_drafts_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_daily_logs_engineer_id",
                table: "daily_logs",
                column: "engineer_id");

            migrationBuilder.CreateIndex(
                name: "ux_daily_logs_project_engineer_date",
                table: "daily_logs",
                columns: new[] { "project_id", "engineer_id", "log_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_day_labour_entries_daily_log_id",
                table: "day_labour_entries",
                column: "daily_log_id");

            migrationBuilder.CreateIndex(
                name: "IX_day_labour_entries_employee_id",
                table: "day_labour_entries",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "IX_day_labour_entries_project_id",
                table: "day_labour_entries",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_executed_quantity_entries_daily_log_id",
                table: "executed_quantity_entries",
                column: "daily_log_id");

            migrationBuilder.CreateIndex(
                name: "IX_executed_quantity_entries_daily_log_id_boq_line_id",
                table: "executed_quantity_entries",
                columns: new[] { "daily_log_id", "boq_line_id" });

            migrationBuilder.CreateIndex(
                name: "IX_executed_quantity_entries_project_id",
                table: "executed_quantity_entries",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_field_entries_daily_log_id",
                table: "field_entries",
                column: "daily_log_id");

            migrationBuilder.CreateIndex(
                name: "IX_field_entries_project_id",
                table: "field_entries",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_material_entries_daily_log_id",
                table: "material_entries",
                column: "daily_log_id");

            migrationBuilder.CreateIndex(
                name: "IX_material_entries_project_id",
                table: "material_entries",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_photo_entries_daily_log_id",
                table: "photo_entries",
                column: "daily_log_id");

            migrationBuilder.CreateIndex(
                name: "IX_photo_entries_project_id",
                table: "photo_entries",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_site_expense_drafts_daily_log_id",
                table: "site_expense_drafts",
                column: "daily_log_id");

            migrationBuilder.CreateIndex(
                name: "IX_site_expense_drafts_project_id",
                table: "site_expense_drafts",
                column: "project_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "day_labour_entries");

            migrationBuilder.DropTable(
                name: "executed_quantity_entries");

            migrationBuilder.DropTable(
                name: "field_entries");

            migrationBuilder.DropTable(
                name: "material_entries");

            migrationBuilder.DropTable(
                name: "photo_entries");

            migrationBuilder.DropTable(
                name: "site_expense_drafts");

            migrationBuilder.DropTable(
                name: "daily_logs");
        }
    }
}
