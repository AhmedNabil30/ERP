using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kaff.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// The three fields spec.md §9's amendment requires on a user: a forced first-sign-in password
    /// change (D-049 ruling 4) and per-account lockout state (D-049 ruling 3).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The scaffolded defaults were checked rather than trusted — <c>false</c>, <c>0</c> and
    /// <c>NULL</c> are all legal values of their columns, so an existing row backfills to a state the
    /// entity can materialise. D-049's migration scaffolded <c>defaultValue: ""</c> onto a
    /// text-stored enum, which is not a member of it and would have failed on the next read; that is
    /// the check being repeated here, not a formality.
    /// </para>
    /// <para>
    /// The backfilled values are also the safe ones: an account that existed before this migration is
    /// not suddenly forced to change a password nobody issued it, and is not part-way through a
    /// failure run it never had. No column is money, so no precision applies.
    /// </para>
    /// <para>
    /// <c>Down</c> is lossless in shape and in data: the three columns hold only in-flight sign-in
    /// state, so nothing of record is lost by dropping them.
    /// </para>
    /// </remarks>
    public partial class UserLockoutAndForcedPasswordChange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "failed_sign_in_attempts",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "locked_out_until",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "must_change_password",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "failed_sign_in_attempts",
                table: "users");

            migrationBuilder.DropColumn(
                name: "locked_out_until",
                table: "users");

            migrationBuilder.DropColumn(
                name: "must_change_password",
                table: "users");
        }
    }
}
