using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace User.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedDefaultRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "CreatedAt", "CreatedBy", "DeletedAt", "Description", "IsActive", "IsDeleted", "RoleCode", "RoleName", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), new DateTimeOffset(new DateTime(2026, 10, 22, 19, 41, 51, 815, DateTimeKind.Unspecified).AddTicks(8885), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000000"), null, "Khách mua hàng và tham gia đấu giá", true, false, "Buyer", "Buyer", new DateTimeOffset(new DateTime(2026, 10, 22, 19, 41, 51, 815, DateTimeKind.Unspecified).AddTicks(8886), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("22222222-2222-2222-2222-222222222222"), new DateTimeOffset(new DateTime(2026, 10, 22, 19, 41, 51, 815, DateTimeKind.Unspecified).AddTicks(8888), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000000"), null, "Người bán hàng và mở phiên đấu giá", true, false, "Seller", "Seller", new DateTimeOffset(new DateTime(2026, 10, 22, 19, 41, 51, 815, DateTimeKind.Unspecified).AddTicks(8888), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("33333333-3333-3333-3333-333333333333"), new DateTimeOffset(new DateTime(2026, 10, 22, 19, 41, 51, 815, DateTimeKind.Unspecified).AddTicks(8891), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000000"), null, "Nhân viên hỗ trợ thẩm định và chăm sóc khách hàng", true, false, "SupportStaff", "Support Staff", new DateTimeOffset(new DateTime(2026, 10, 22, 19, 41, 51, 815, DateTimeKind.Unspecified).AddTicks(8891), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("44444444-4444-4444-4444-444444444444"), new DateTimeOffset(new DateTime(2026, 10, 22, 19, 41, 51, 815, DateTimeKind.Unspecified).AddTicks(8893), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000000"), null, "Quản trị viên tối cao sàn", true, false, "Admin", "Administrator", new DateTimeOffset(new DateTime(2026, 10, 22, 19, 41, 51, 815, DateTimeKind.Unspecified).AddTicks(8894), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000000") }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"));

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"));

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"));
        }
    }
}
