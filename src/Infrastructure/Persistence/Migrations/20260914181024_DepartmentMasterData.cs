using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Kaff.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DepartmentMasterData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // KAFF-321, decisions.md D-162 — Department becomes master data. The Departments table and
            // its seed are created BEFORE the old `department` column is touched, so the backfill below
            // has something to join against.
            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "Departments",
                columns: new[] { "id", "is_active", "name_ar", "name_en" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000201"), true, "المالية", "Finance" },
                    { new Guid("00000000-0000-0000-0000-000000000202"), true, "المكتب الفني", "Technical Office" },
                    { new Guid("00000000-0000-0000-0000-000000000203"), true, "العمليات", "Operations" },
                    { new Guid("00000000-0000-0000-0000-000000000204"), true, "المشتريات", "Procurement" },
                    { new Guid("00000000-0000-0000-0000-000000000205"), true, "الموارد البشرية", "HR" }
                });

            migrationBuilder.DropCheckConstraint(
                name: "ck_users_operations_sub_department",
                table: "users");

            migrationBuilder.AddColumn<Guid>(
                name: "department_id",
                table: "users",
                type: "uuid",
                nullable: true);

            // Backfill: the D-153 §2 enum's three names that survive under D-162 map straight across.
            // `Marketing` has no successor in the five seeded departments (D-162 names only Finance,
            // Technical Office, Operations, Procurement, HR) — any user carrying it is left with
            // department_id NULL rather than pointed at an invented row. Flagged in decisions.md rather
            // than guessed further; this system has no production Marketing-department user yet.
            migrationBuilder.Sql(
                "UPDATE users SET department_id = '00000000-0000-0000-0000-000000000201' WHERE department = 'Finance';");
            migrationBuilder.Sql(
                "UPDATE users SET department_id = '00000000-0000-0000-0000-000000000203' WHERE department = 'Operations';");
            migrationBuilder.Sql(
                "UPDATE users SET department_id = '00000000-0000-0000-0000-000000000205' WHERE department = 'Hr';");

            migrationBuilder.DropColumn(
                name: "department",
                table: "users");

            migrationBuilder.CreateIndex(
                name: "IX_users_department_id",
                table: "users",
                column: "department_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_operations_sub_department",
                table: "users",
                sql: "(department_id = '00000000-0000-0000-0000-000000000203' AND operations_sub_department IS NOT NULL) OR (department_id IS DISTINCT FROM '00000000-0000-0000-0000-000000000203' AND operations_sub_department IS NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_users_Departments_department_id",
                table: "users",
                column: "department_id",
                principalTable: "Departments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_users_Departments_department_id",
                table: "users");

            migrationBuilder.DropTable(
                name: "Departments");

            migrationBuilder.DropIndex(
                name: "IX_users_department_id",
                table: "users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_users_operations_sub_department",
                table: "users");

            migrationBuilder.DropColumn(
                name: "department_id",
                table: "users");

            migrationBuilder.AddColumn<string>(
                name: "department",
                table: "users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_operations_sub_department",
                table: "users",
                sql: "(department = 'Operations' AND operations_sub_department IS NOT NULL) OR (department IS DISTINCT FROM 'Operations' AND operations_sub_department IS NULL)");
        }
    }
}
