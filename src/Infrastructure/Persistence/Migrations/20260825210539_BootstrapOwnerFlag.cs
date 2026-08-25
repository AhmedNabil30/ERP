using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kaff.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// KAFF-100 rule 6 — the database-enforced half of "the check and the insert are one atomic
    /// operation."
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>is_bootstrap_owner</c> backfills to <c>false</c> on every existing row, which is the safe
    /// value: nothing before this migration was created through the setup screen, so nothing should
    /// retroactively claim the flag. <c>ux_users_bootstrap_owner_once</c> then permits at most one row
    /// with the flag set, for the life of the table — the mechanism that turns two concurrent
    /// <c>POST /api/setup</c> requests into one Owner and one unique-violation, whichever request's own
    /// <c>Users.AnyAsync()</c> read got there first. See <c>User.IsBootstrapOwner</c>.
    /// </para>
    /// <para><c>Down</c> is lossless: the column carries no data that exists independently of it.</para>
    /// </remarks>
    public partial class BootstrapOwnerFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_bootstrap_owner",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ux_users_bootstrap_owner_once",
                table: "users",
                column: "is_bootstrap_owner",
                unique: true,
                filter: "is_bootstrap_owner = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_users_bootstrap_owner_once",
                table: "users");

            migrationBuilder.DropColumn(
                name: "is_bootstrap_owner",
                table: "users");
        }
    }
}
