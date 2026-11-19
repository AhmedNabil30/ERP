using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kaff.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExtractRevisionAndRejectionKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "revises_extract_id",
                table: "extracts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rejection_kind",
                table: "extract_gate_records",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_extracts_revises_extract_id",
                table: "extracts",
                column: "revises_extract_id",
                unique: true,
                filter: "revises_extract_id IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_extracts_extracts_revises_extract_id",
                table: "extracts",
                column: "revises_extract_id",
                principalTable: "extracts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_extracts_extracts_revises_extract_id",
                table: "extracts");

            migrationBuilder.DropIndex(
                name: "ux_extracts_revises_extract_id",
                table: "extracts");

            migrationBuilder.DropColumn(
                name: "revises_extract_id",
                table: "extracts");

            migrationBuilder.DropColumn(
                name: "rejection_kind",
                table: "extract_gate_records");
        }
    }
}
