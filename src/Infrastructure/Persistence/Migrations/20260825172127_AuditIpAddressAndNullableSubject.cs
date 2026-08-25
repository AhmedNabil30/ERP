using System;
using System.Net;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kaff.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuditIpAddressAndNullableSubject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "entity_id",
                table: "audit_records",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<IPAddress>(
                name: "ip_address",
                table: "audit_records",
                type: "inet",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_records_entity_change_has_subject",
                table: "audit_records",
                sql: "action = 'Occurred' OR entity_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_records_entity_change_has_subject",
                table: "audit_records");

            migrationBuilder.DropColumn(
                name: "ip_address",
                table: "audit_records");

            migrationBuilder.AlterColumn<Guid>(
                name: "entity_id",
                table: "audit_records",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
