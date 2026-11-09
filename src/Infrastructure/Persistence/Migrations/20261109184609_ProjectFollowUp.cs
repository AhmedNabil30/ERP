using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kaff.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProjectFollowUp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "follow_up_started_on",
                table: "projects",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            // Existing projects start their follow-up period at creation (Q-208-2).
            migrationBuilder.Sql("UPDATE projects SET follow_up_started_on = created_at;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "follow_up_started_on",
                table: "projects");
        }
    }
}
