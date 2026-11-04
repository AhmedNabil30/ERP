using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kaff.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CollectionsAndCheques : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "collection_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    extract_id = table.Column<Guid>(type: "uuid", nullable: false),
                    method = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cash_received = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    withheld_at_source = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    recorded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_collection_records", x => x.id);
                    table.ForeignKey(
                        name: "FK_collection_records_extracts_extract_id",
                        column: x => x.extract_id,
                        principalTable: "extracts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_collection_records_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_collection_records_users_recorded_by_user_id",
                        column: x => x.recorded_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cheques",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    collection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    state = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    state_changed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    state_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cheques", x => x.id);
                    table.ForeignKey(
                        name: "FK_cheques_collection_records_collection_id",
                        column: x => x.collection_id,
                        principalTable: "collection_records",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cheques_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cheques_users_state_changed_by_user_id",
                        column: x => x.state_changed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cheques_collection_id",
                table: "cheques",
                column: "collection_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cheques_project_id",
                table: "cheques",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_cheques_state_changed_by_user_id",
                table: "cheques",
                column: "state_changed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_collection_records_extract_id",
                table: "collection_records",
                column: "extract_id");

            migrationBuilder.CreateIndex(
                name: "IX_collection_records_project_id",
                table: "collection_records",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_collection_records_recorded_by_user_id",
                table: "collection_records",
                column: "recorded_by_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cheques");

            migrationBuilder.DropTable(
                name: "collection_records");
        }
    }
}
