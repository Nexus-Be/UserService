using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using User.Infrastructure.Persistence;

#nullable disable

namespace User.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lets a numeric setting carry the lowest and highest value an administrator may enter.
    /// Idempotent because shared databases do not follow this migration history.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261121110000_AddSystemConfigRange")]
    public partial class AddSystemConfigRange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE ""SystemConfigs"" ADD COLUMN IF NOT EXISTS ""MinValue"" numeric NULL;
ALTER TABLE ""SystemConfigs"" ADD COLUMN IF NOT EXISTS ""MaxValue"" numeric NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE ""SystemConfigs"" DROP COLUMN IF EXISTS ""MaxValue"";
ALTER TABLE ""SystemConfigs"" DROP COLUMN IF EXISTS ""MinValue"";");
        }
    }
}
