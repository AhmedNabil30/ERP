using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kaff.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EngagementHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "engagements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day_rate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    opened_on = table.Column<DateOnly>(type: "date", nullable: false),
                    opened_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    closed_on = table.Column<DateOnly>(type: "date", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    rating = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_engagements", x => x.id);
                    table.CheckConstraint("ck_engagements_closed_not_before_opened", "closed_on IS NULL OR closed_on >= opened_on");
                    table.CheckConstraint("ck_engagements_closed_shape", "(closed_on IS NULL) = (closed_at IS NULL)");
                    table.CheckConstraint("ck_engagements_rating_range", "rating IS NULL OR (rating >= 1 AND rating <= 5)");
                    table.ForeignKey(
                        name: "FK_engagements_employees_worker_id",
                        column: x => x.worker_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_engagements_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_engagements_project_opened",
                table: "engagements",
                columns: new[] { "project_id", "opened_at" });

            migrationBuilder.CreateIndex(
                name: "ix_engagements_worker_opened",
                table: "engagements",
                columns: new[] { "worker_id", "opened_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "engagements");
        }
    }
}
