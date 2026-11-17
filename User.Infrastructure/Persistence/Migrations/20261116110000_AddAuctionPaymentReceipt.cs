using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using User.Infrastructure.Persistence;

#nullable disable

namespace User.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261116110000_AddAuctionPaymentReceipt")]
public sealed class AddAuctionPaymentReceipt : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("UserAuctionPayments", columns: table => new
        {
            Id = table.Column<Guid>(type: "uuid", nullable: false),
            UserId = table.Column<Guid>(type: "uuid", nullable: false),
            AuctionId = table.Column<Guid>(type: "uuid", nullable: false),
            OrderId = table.Column<Guid>(type: "uuid", nullable: false),
            Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
            PaidAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            PreviousStreak = table.Column<int>(type: "integer", nullable: false),
            RestrictionLifted = table.Column<bool>(type: "boolean", nullable: false),
            CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
            UpdatedBy = table.Column<Guid>(type: "uuid", nullable: false),
            IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
            DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
        }, constraints: table => table.PrimaryKey("PK_UserAuctionPayments", x => x.Id));
        migrationBuilder.CreateIndex("IX_UserAuctionPayments_OrderId", "UserAuctionPayments", "OrderId", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("UserAuctionPayments");
}
