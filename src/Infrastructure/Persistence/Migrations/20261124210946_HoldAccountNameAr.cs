using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kaff.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HoldAccountNameAr : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // D-274: the hold account reads «الحجز». The account name only; no posting is touched.
            migrationBuilder.Sql("UPDATE accounts SET name_ar = 'الحجز' WHERE code LIKE '%-HOLD' AND name_ar = 'محجوز';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE accounts SET name_ar = 'محجوز' WHERE code LIKE '%-HOLD' AND name_ar = 'الحجز';");
        }
    }
}
