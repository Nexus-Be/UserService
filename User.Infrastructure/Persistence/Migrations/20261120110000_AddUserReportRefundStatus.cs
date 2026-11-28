using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using User.Infrastructure.Persistence;

#nullable disable

namespace User.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Tracks the refund owed to the buyer when a dispute is decided in their favour (None, Pending, Refunded, ManualProcessingRequired, Failed).
    /// Idempotent because shared databases do not follow this migration history.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261120110000_AddUserReportRefundStatus")]
    public partial class AddUserReportRefundStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE ""UserReports"" ADD COLUMN IF NOT EXISTS ""RefundStatus"" integer NOT NULL DEFAULT 0;
ALTER TABLE ""UserReports"" ADD COLUMN IF NOT EXISTS ""RefundAmount"" numeric(18,2) NULL;
ALTER TABLE ""UserReports"" ADD COLUMN IF NOT EXISTS ""RefundMessage"" character varying(500) NULL;
CREATE INDEX IF NOT EXISTS ""IX_UserReports_RefundStatus"" ON ""UserReports"" (""RefundStatus"");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP INDEX IF EXISTS ""IX_UserReports_RefundStatus"";
ALTER TABLE ""UserReports"" DROP COLUMN IF EXISTS ""RefundMessage"";
ALTER TABLE ""UserReports"" DROP COLUMN IF EXISTS ""RefundAmount"";
ALTER TABLE ""UserReports"" DROP COLUMN IF EXISTS ""RefundStatus"";");
        }
    }
}
