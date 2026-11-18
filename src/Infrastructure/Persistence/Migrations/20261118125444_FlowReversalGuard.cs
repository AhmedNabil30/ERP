using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kaff.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// D-236: a flow's posting is reversed by that flow. Installs <c>006_flow_reversal.sql</c>, so a database built by
    /// migrations alone carries the guard (D-038). No schema change.
    /// </summary>
    public partial class FlowReversalGuard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(Kaff.Infrastructure.Persistence.GuardScripts.Read("006_flow_reversal.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_postings_flow_reversal ON postings;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS kaff_flow_reversal_owned();");
        }
    }
}
