using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kaff.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AccountingLayer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "accounting_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payload = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    decided_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounting_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_accounting_requests_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_accounting_requests_users_decided_by_user_id",
                        column: x => x.decided_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_accounting_requests_users_requested_by_user_id",
                        column: x => x.requested_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "accruals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    accrued_expense_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expense_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    accrued_on = table.Column<DateOnly>(type: "date", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    released_on = table.Column<DateOnly>(type: "date", nullable: true),
                    released_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accruals", x => x.id);
                    table.ForeignKey(
                        name: "FK_accruals_accounts_accrued_expense_account_id",
                        column: x => x.accrued_expense_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_accruals_accounts_expense_account_id",
                        column: x => x.expense_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_accruals_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_accruals_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_accruals_users_released_by_user_id",
                        column: x => x.released_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "asset_register_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    cost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    rate = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    purchase_date = table.Column<DateOnly>(type: "date", nullable: false),
                    accumulated_depreciation_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    depreciation_expense_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_asset_register_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_asset_register_entries_accounts_accumulated_depreciation_ac~",
                        column: x => x.accumulated_depreciation_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_asset_register_entries_accounts_depreciation_expense_accoun~",
                        column: x => x.depreciation_expense_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_asset_register_entries_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "budget_baselines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_boq_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cost_total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tolerance = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    set_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    set_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_budget_baselines", x => x.id);
                    table.ForeignKey(
                        name: "FK_budget_baselines_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_budget_baselines_users_set_by_user_id",
                        column: x => x.set_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "prepayment_schedules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    prepayment_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expense_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    funding_posting_id = table.Column<Guid>(type: "uuid", nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    months = table.Column<int>(type: "integer", nullable: false),
                    first_month = table.Column<DateOnly>(type: "date", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prepayment_schedules", x => x.id);
                    table.ForeignKey(
                        name: "FK_prepayment_schedules_accounts_expense_account_id",
                        column: x => x.expense_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_prepayment_schedules_accounts_prepayment_account_id",
                        column: x => x.prepayment_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_prepayment_schedules_postings_funding_posting_id",
                        column: x => x.funding_posting_id,
                        principalTable: "postings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_prepayment_schedules_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_prepayment_schedules_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_accounting_requests_decided_by_user_id",
                table: "accounting_requests",
                column: "decided_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_accounting_requests_project_id",
                table: "accounting_requests",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_accounting_requests_requested_by_user_id",
                table: "accounting_requests",
                column: "requested_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_accounting_requests_status",
                table: "accounting_requests",
                columns: new[] { "status", "requested_at" });

            migrationBuilder.CreateIndex(
                name: "IX_accruals_accrued_expense_account_id",
                table: "accruals",
                column: "accrued_expense_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_accruals_created_by_user_id",
                table: "accruals",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_accruals_expense_account_id",
                table: "accruals",
                column: "expense_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_accruals_project_id",
                table: "accruals",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_accruals_released_by_user_id",
                table: "accruals",
                column: "released_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_accruals_released_on",
                table: "accruals",
                column: "released_on");

            migrationBuilder.CreateIndex(
                name: "IX_asset_register_entries_accumulated_depreciation_account_id",
                table: "asset_register_entries",
                column: "accumulated_depreciation_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_asset_register_entries_created_by_user_id",
                table: "asset_register_entries",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_asset_register_entries_depreciation_expense_account_id",
                table: "asset_register_entries",
                column: "depreciation_expense_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_budget_baselines_set_by_user_id",
                table: "budget_baselines",
                column: "set_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_budget_baselines_project",
                table: "budget_baselines",
                column: "project_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_prepayment_schedules_created_by_user_id",
                table: "prepayment_schedules",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_prepayment_schedules_expense_account_id",
                table: "prepayment_schedules",
                column: "expense_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_prepayment_schedules_prepayment_account_id",
                table: "prepayment_schedules",
                column: "prepayment_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_prepayment_schedules_project_id",
                table: "prepayment_schedules",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ux_prepayment_schedules_funding",
                table: "prepayment_schedules",
                column: "funding_posting_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "accounting_requests");

            migrationBuilder.DropTable(
                name: "accruals");

            migrationBuilder.DropTable(
                name: "asset_register_entries");

            migrationBuilder.DropTable(
                name: "budget_baselines");

            migrationBuilder.DropTable(
                name: "prepayment_schedules");
        }
    }
}
