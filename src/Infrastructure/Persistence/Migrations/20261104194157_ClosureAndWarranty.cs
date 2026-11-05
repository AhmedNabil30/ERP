using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kaff.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClosureAndWarranty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "snags",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    owner_kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    subcontractor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    priority = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    raised_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    raised_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    fixed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fixed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    verified_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolution_kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    resolution_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    resolved_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_snags", x => x.id);
                    table.ForeignKey(
                        name: "FK_snags_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_snags_subcontractors_subcontractor_id",
                        column: x => x.subcontractor_id,
                        principalTable: "subcontractors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_snags_users_fixed_by_user_id",
                        column: x => x.fixed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_snags_users_raised_by_user_id",
                        column: x => x.raised_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_snags_users_resolved_by_user_id",
                        column: x => x.resolved_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_snags_users_verified_by_user_id",
                        column: x => x.verified_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "warranty_callbacks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    raised_on = table.Column<DateOnly>(type: "date", nullable: false),
                    raised_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    raised_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    resolved_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warranty_callbacks", x => x.id);
                    table.ForeignKey(
                        name: "FK_warranty_callbacks_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_warranty_callbacks_users_raised_by_user_id",
                        column: x => x.raised_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_warranty_callbacks_users_resolved_by_user_id",
                        column: x => x.resolved_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_snags_fixed_by_user_id",
                table: "snags",
                column: "fixed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_snags_raised_by_user_id",
                table: "snags",
                column: "raised_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_snags_resolved_by_user_id",
                table: "snags",
                column: "resolved_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_snags_subcontractor_id",
                table: "snags",
                column: "subcontractor_id");

            migrationBuilder.CreateIndex(
                name: "IX_snags_verified_by_user_id",
                table: "snags",
                column: "verified_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_snags_project_number",
                table: "snags",
                columns: new[] { "project_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_warranty_callbacks_raised_by_user_id",
                table: "warranty_callbacks",
                column: "raised_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_warranty_callbacks_resolved_by_user_id",
                table: "warranty_callbacks",
                column: "resolved_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_warranty_callbacks_project_number",
                table: "warranty_callbacks",
                columns: new[] { "project_id", "number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "snags");

            migrationBuilder.DropTable(
                name: "warranty_callbacks");
        }
    }
}
