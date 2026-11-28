using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using User.Infrastructure.Persistence;

#nullable disable

namespace User.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Marks sellers whose warehouse could not be created in Fulfillment at approval time (WarehouseReady = false) so they can be retried and listed.
    /// Existing sellers default to true: this migration does not know which of them lack a warehouse (that comes from the read-only reconciliation queries).
    /// Idempotent because shared databases do not follow this migration history.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261121090000_AddSellerProfileWarehouseReady")]
    public partial class AddSellerProfileWarehouseReady : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE ""SellerProfiles"" ADD COLUMN IF NOT EXISTS ""WarehouseReady"" boolean NOT NULL DEFAULT TRUE;
CREATE INDEX IF NOT EXISTS ""IX_SellerProfiles_WarehouseReady"" ON ""SellerProfiles"" (""WarehouseReady"") WHERE ""WarehouseReady"" = FALSE;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP INDEX IF EXISTS ""IX_SellerProfiles_WarehouseReady"";
ALTER TABLE ""SellerProfiles"" DROP COLUMN IF EXISTS ""WarehouseReady"";");
        }
    }
}
