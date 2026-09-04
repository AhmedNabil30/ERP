using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kaff.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClientCodeSequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "client_code_seq",
                startValue: 10001L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropSequence(
                name: "client_code_seq");
        }
    }
}
