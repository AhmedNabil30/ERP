using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kaff.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "accounting_periods",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    month = table.Column<int>(type: "integer", nullable: false),
                    starts_on = table.Column<DateOnly>(type: "date", nullable: false),
                    ends_on = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounting_periods", x => x.id);
                    table.CheckConstraint("ck_accounting_periods_month", "month BETWEEN 1 AND 12");
                    table.CheckConstraint("ck_accounting_periods_range", "ends_on >= starts_on");
                });

            migrationBuilder.CreateTable(
                name: "accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    @class = table.Column<string>(name: "class", type: "character varying(64)", maxLength: 64, nullable: false),
                    normal_balance = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ledger_kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    is_postable = table.Column<bool>(type: "boolean", nullable: false),
                    enforce_non_negative = table.Column<bool>(type: "boolean", nullable: false),
                    currency = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    party_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    party_id = table.Column<Guid>(type: "uuid", nullable: true),
                    parent_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    opened_on = table.Column<DateOnly>(type: "date", nullable: false),
                    closed_on = table.Column<DateOnly>(type: "date", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.id);
                    table.CheckConstraint("ck_accounts_closed_after_opened", "closed_on IS NULL OR closed_on >= opened_on");
                    table.CheckConstraint("ck_accounts_ledger_is_postable", "ledger_kind IS NULL OR is_postable = TRUE");
                    table.CheckConstraint("ck_accounts_party_complete", "(party_type IS NULL AND party_id IS NULL) OR (party_type IS NOT NULL AND party_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_accounts_accounts_parent_account_id",
                        column: x => x.parent_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "audit_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    actor_role = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    before_json = table.Column<string>(type: "jsonb", nullable: true),
                    after_json = table.Column<string>(type: "jsonb", nullable: true),
                    changed_properties = table.Column<string>(type: "jsonb", nullable: false),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    request_path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_records", x => x.id);
                    table.CheckConstraint("ck_audit_records_has_state", "before_json IS NOT NULL OR after_json IS NOT NULL");
                });

            migrationBuilder.CreateTable(
                name: "babs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    parent_bab_id = table.Column<Guid>(type: "uuid", nullable: true),
                    default_markup = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_babs", x => x.id);
                    table.CheckConstraint("ck_babs_not_own_parent", "parent_bab_id IS NULL OR parent_bab_id <> id");
                    table.ForeignKey(
                        name: "FK_babs_babs_parent_bab_id",
                        column: x => x.parent_bab_id,
                        principalTable: "babs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "clients",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    phone_entered = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    phone_normalised = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    alternate_phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    address = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    withholding_category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tax_registration_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clients", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "suppliers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    phone_entered = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    phone_normalised = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    withholding_category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tax_registration_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    address = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_suppliers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "postings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    posting_date = table.Column<DateOnly>(type: "date", nullable: false),
                    from_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    source_document_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    source_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_document_reference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reverses_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_postings", x => x.id);
                    table.CheckConstraint("ck_postings_amount_positive", "amount > 0");
                    table.CheckConstraint("ck_postings_distinct_accounts", "from_account_id <> to_account_id");
                    table.CheckConstraint("ck_postings_not_self_reversing", "reverses_id IS NULL OR reverses_id <> id");
                    table.ForeignKey(
                        name: "FK_postings_accounts_from_account_id",
                        column: x => x.from_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_postings_accounts_to_account_id",
                        column: x => x.to_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_postings_postings_reverses_id",
                        column: x => x.reverses_id,
                        principalTable: "postings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "catalogue_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    description_ar = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    description_en = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    unit = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    bab_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cost_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    base_sell_rate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalogue_items", x => x.id);
                    table.CheckConstraint("ck_catalogue_items_cost_not_negative", "cost_price >= 0");
                    table.CheckConstraint("ck_catalogue_items_rate_not_negative", "base_sell_rate >= 0");
                    table.ForeignKey(
                        name: "FK_catalogue_items_babs_bab_id",
                        column: x => x.bab_id,
                        principalTable: "babs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "employees",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    phone_entered = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    phone_normalised = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    bab_id = table.Column<Guid>(type: "uuid", nullable: true),
                    specialty = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    national_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    job_title = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    hired_on = table.Column<DateOnly>(type: "date", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employees", x => x.id);
                    table.CheckConstraint("ck_employees_day_labour_has_trade", "kind <> 'DayLabour' OR bab_id IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_employees_babs_bab_id",
                        column: x => x.bab_id,
                        principalTable: "babs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "subcontractors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    phone_entered = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    phone_normalised = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    trade_bab_id = table.Column<Guid>(type: "uuid", nullable: true),
                    retention_rate = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    withholding_category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tax_registration_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subcontractors", x => x.id);
                    table.ForeignKey(
                        name: "FK_subcontractors_babs_trade_bab_id",
                        column: x => x.trade_bab_id,
                        principalTable: "babs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "opportunities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    stage = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    closed_lost_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    last_activity_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    converted_project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_opportunities", x => x.id);
                    table.CheckConstraint("ck_opportunities_closed_lost_reason", "status <> 'ClosedLost' OR closed_lost_reason IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_opportunities_clients_client_id",
                        column: x => x.client_id,
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    phone_entered = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    phone_normalised = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    role = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    department = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    operations_sub_department = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    client_id = table.Column<Guid>(type: "uuid", nullable: true),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: true),
                    password_hash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    security_stamp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deactivated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                    table.CheckConstraint("ck_users_client_scope", "(role = 'Client' AND client_id IS NOT NULL) OR (role <> 'Client' AND client_id IS NULL)");
                    table.CheckConstraint("ck_users_operations_sub_department", "(department = 'Operations' AND operations_sub_department IS NOT NULL) OR (department IS DISTINCT FROM 'Operations' AND operations_sub_department IS NULL)");
                    table.CheckConstraint("ck_users_subcontractor_cannot_log_in", "role <> 'Subcontractor' OR password_hash IS NULL");
                    table.ForeignKey(
                        name: "FK_users_clients_client_id",
                        column: x => x.client_id,
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_users_employees_employee_id",
                        column: x => x.employee_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "projects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    opportunity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    contract_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    currency = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    contract_value = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    advance_rate = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    hold_rate = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    advance_recovery_rate = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    material_advance_rate = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    delay_penalty_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    supervision_rate = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    area_square_metres = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    design_rate_per_square_metre = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    signed_on = table.Column<DateOnly>(type: "date", nullable: true),
                    started_on = table.Column<DateOnly>(type: "date", nullable: true),
                    handover_on = table.Column<DateOnly>(type: "date", nullable: true),
                    warranty_ends_on = table.Column<DateOnly>(type: "date", nullable: true),
                    closed_on = table.Column<DateOnly>(type: "date", nullable: true),
                    stopped_on = table.Column<DateOnly>(type: "date", nullable: true),
                    stoppage_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    terminated_on = table.Column<DateOnly>(type: "date", nullable: true),
                    termination_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    linked_project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    link_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projects", x => x.id);
                    table.CheckConstraint("ck_projects_area_positive", "area_square_metres IS NULL OR area_square_metres > 0");
                    table.CheckConstraint("ck_projects_cost_plus_terms", "contract_type = 'CostPlus' OR supervision_rate IS NULL");
                    table.CheckConstraint("ck_projects_design_terms", "contract_type = 'Design' OR (area_square_metres IS NULL AND design_rate_per_square_metre IS NULL)");
                    table.CheckConstraint("ck_projects_link_complete", "(linked_project_id IS NULL AND link_type IS NULL) OR (linked_project_id IS NOT NULL AND link_type IS NOT NULL)");
                    table.CheckConstraint("ck_projects_lump_sum_terms", "contract_type = 'LumpSum' OR (advance_rate IS NULL AND hold_rate IS NULL AND advance_recovery_rate IS NULL AND material_advance_rate IS NULL)");
                    table.CheckConstraint("ck_projects_not_linked_to_itself", "linked_project_id IS NULL OR linked_project_id <> id");
                    table.CheckConstraint("ck_projects_stoppage_reason", "stopped_on IS NULL OR stoppage_reason IS NOT NULL");
                    table.CheckConstraint("ck_projects_termination_reason", "terminated_on IS NULL OR termination_reason IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_projects_clients_client_id",
                        column: x => x.client_id,
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_projects_opportunities_opportunity_id",
                        column: x => x.opportunity_id,
                        principalTable: "opportunities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_projects_projects_linked_project_id",
                        column: x => x.linked_project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "project_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    level = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    assigned_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_assignments", x => x.id);
                    table.CheckConstraint("ck_project_assignments_revocation_complete", "(revoked_at IS NULL AND revoked_by_user_id IS NULL) OR (revoked_at IS NOT NULL AND revoked_by_user_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_project_assignments_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_assignments_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_accounting_periods_status_range",
                table: "accounting_periods",
                columns: new[] { "status", "starts_on", "ends_on" });

            migrationBuilder.CreateIndex(
                name: "ux_accounting_periods_year_month",
                table: "accounting_periods",
                columns: new[] { "year", "month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_accounts_ledger_kind",
                table: "accounts",
                column: "ledger_kind");

            migrationBuilder.CreateIndex(
                name: "IX_accounts_parent_account_id",
                table: "accounts",
                column: "parent_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_accounts_party",
                table: "accounts",
                columns: new[] { "party_type", "party_id" });

            migrationBuilder.CreateIndex(
                name: "ix_accounts_project_type",
                table: "accounts",
                columns: new[] { "project_id", "type" });

            migrationBuilder.CreateIndex(
                name: "ux_accounts_code",
                table: "accounts",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_records_actor",
                table: "audit_records",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_records_correlation",
                table: "audit_records",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_records_entity",
                table: "audit_records",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_records_occurred_at",
                table: "audit_records",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "ix_audit_records_project",
                table: "audit_records",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_babs_parent_bab_id",
                table: "babs",
                column: "parent_bab_id");

            migrationBuilder.CreateIndex(
                name: "ux_babs_code",
                table: "babs",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_catalogue_items_bab",
                table: "catalogue_items",
                column: "bab_id");

            migrationBuilder.CreateIndex(
                name: "ux_catalogue_items_code",
                table: "catalogue_items",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_clients_code",
                table: "clients",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_clients_phone",
                table: "clients",
                column: "phone_normalised",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_employees_bab_id",
                table: "employees",
                column: "bab_id");

            migrationBuilder.CreateIndex(
                name: "ux_employees_code",
                table: "employees",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_employees_phone",
                table: "employees",
                column: "phone_normalised",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_opportunities_activity",
                table: "opportunities",
                columns: new[] { "status", "last_activity_at" });

            migrationBuilder.CreateIndex(
                name: "ix_opportunities_client",
                table: "opportunities",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "ux_opportunities_code",
                table: "opportunities",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_postings_from_account_date",
                table: "postings",
                columns: new[] { "from_account_id", "posting_date" });

            migrationBuilder.CreateIndex(
                name: "ix_postings_project_date",
                table: "postings",
                columns: new[] { "project_id", "posting_date" });

            migrationBuilder.CreateIndex(
                name: "ix_postings_source_document",
                table: "postings",
                columns: new[] { "source_document_type", "source_document_id" });

            migrationBuilder.CreateIndex(
                name: "ix_postings_to_account_date",
                table: "postings",
                columns: new[] { "to_account_id", "posting_date" });

            migrationBuilder.CreateIndex(
                name: "ux_postings_reverses",
                table: "postings",
                column: "reverses_id",
                unique: true,
                filter: "reverses_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_project_assignments_user_project",
                table: "project_assignments",
                columns: new[] { "user_id", "project_id" });

            migrationBuilder.CreateIndex(
                name: "ux_project_assignments_active",
                table: "project_assignments",
                columns: new[] { "project_id", "user_id" },
                unique: true,
                filter: "revoked_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_projects_client",
                table: "projects",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "IX_projects_linked_project_id",
                table: "projects",
                column: "linked_project_id");

            migrationBuilder.CreateIndex(
                name: "IX_projects_opportunity_id",
                table: "projects",
                column: "opportunity_id");

            migrationBuilder.CreateIndex(
                name: "ix_projects_status",
                table: "projects",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ux_projects_code",
                table: "projects",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_subcontractors_trade_bab_id",
                table: "subcontractors",
                column: "trade_bab_id");

            migrationBuilder.CreateIndex(
                name: "ux_subcontractors_code",
                table: "subcontractors",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_subcontractors_phone",
                table: "subcontractors",
                column: "phone_normalised",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_suppliers_code",
                table: "suppliers",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_suppliers_phone",
                table: "suppliers",
                column: "phone_normalised",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_client_id",
                table: "users",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_employee_id",
                table: "users",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_phone_normalised",
                table: "users",
                column: "phone_normalised");

            migrationBuilder.CreateIndex(
                name: "ux_users_user_name",
                table: "users",
                column: "user_name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "accounting_periods");

            migrationBuilder.DropTable(
                name: "audit_records");

            migrationBuilder.DropTable(
                name: "catalogue_items");

            migrationBuilder.DropTable(
                name: "postings");

            migrationBuilder.DropTable(
                name: "project_assignments");

            migrationBuilder.DropTable(
                name: "subcontractors");

            migrationBuilder.DropTable(
                name: "suppliers");

            migrationBuilder.DropTable(
                name: "accounts");

            migrationBuilder.DropTable(
                name: "projects");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "opportunities");

            migrationBuilder.DropTable(
                name: "employees");

            migrationBuilder.DropTable(
                name: "clients");

            migrationBuilder.DropTable(
                name: "babs");
        }
    }
}
