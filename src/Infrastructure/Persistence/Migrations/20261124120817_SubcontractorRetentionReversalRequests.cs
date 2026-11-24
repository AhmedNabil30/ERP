using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kaff.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SubcontractorRetentionReversalRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "subcontractor_retention_reversal_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subcontractor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_posting_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    decided_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subcontractor_retention_reversal_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_subcontractor_retention_reversal_requests_postings_original~",
                        column: x => x.original_posting_id,
                        principalTable: "postings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_subcontractor_retention_reversal_requests_projects_project_~",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_subcontractor_retention_reversal_requests_users_decided_by_~",
                        column: x => x.decided_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_subcontractor_retention_reversal_requests_users_requested_b~",
                        column: x => x.requested_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_subcontractor_retention_reversal_requests_decided_by_user_id",
                table: "subcontractor_retention_reversal_requests",
                column: "decided_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_subcontractor_retention_reversal_requests_original_posting_~",
                table: "subcontractor_retention_reversal_requests",
                column: "original_posting_id");

            migrationBuilder.CreateIndex(
                name: "IX_subcontractor_retention_reversal_requests_requested_by_user~",
                table: "subcontractor_retention_reversal_requests",
                column: "requested_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_subcontractor_retention_reversal_requests_waiting",
                table: "subcontractor_retention_reversal_requests",
                columns: new[] { "project_id", "subcontractor_id" },
                unique: true,
                filter: "status = 'Requested'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "subcontractor_retention_reversal_requests");
        }
    }
}
