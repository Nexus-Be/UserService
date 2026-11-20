using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using User.Infrastructure.Persistence;

#nullable disable

namespace User.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Adds the unique index that stops one penalty or reward from being applied twice for the same reference.
    /// If old rows already break the rule the index is NOT created (and the migration does not fail): the duplicates must be reviewed first.
    /// List them with:
    ///   SELECT "UserId","TargetRole","ReferenceType","ReferenceId","Reason", count(*) FROM "ReputationLogs"
    ///   WHERE "ReferenceId" IS NOT NULL GROUP BY 1,2,3,4,5 HAVING count(*) > 1;
    /// No reputation row is deleted here.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261120120000_AddReputationLogIdempotencyIndex")]
    public partial class AddReputationLogIdempotencyIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM ""ReputationLogs""
        WHERE ""ReferenceId"" IS NOT NULL
        GROUP BY ""UserId"", ""TargetRole"", ""ReferenceType"", ""ReferenceId"", ""Reason""
        HAVING count(*) > 1) THEN
        RAISE WARNING 'IX_ReputationLogs_Idempotency was not created: duplicate reputation log rows exist and must be reviewed first.';
    ELSE
        CREATE UNIQUE INDEX IF NOT EXISTS ""IX_ReputationLogs_Idempotency""
            ON ""ReputationLogs"" (""UserId"", ""TargetRole"", ""ReferenceType"", ""ReferenceId"", ""Reason"")
            WHERE ""ReferenceId"" IS NOT NULL;
    END IF;
END $$;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_ReputationLogs_Idempotency"";");
        }
    }
}
