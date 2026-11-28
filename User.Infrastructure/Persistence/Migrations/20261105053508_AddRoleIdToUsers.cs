using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace User.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleIdToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RoleId",
                table: "Users",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            // Migrate existing user roles from UserRoles table to Users.RoleId (Priority: Admin > SupportStaff > Seller > Buyer)
            migrationBuilder.Sql(@"
                UPDATE ""Users"" u
                SET ""RoleId"" = sub.""RoleId""
                FROM (
                    SELECT DISTINCT ON (ur.""UserId"") ur.""UserId"", ur.""RoleId""
                    FROM ""UserRoles"" ur
                    INNER JOIN ""Roles"" r ON ur.""RoleId"" = r.""Id""
                    WHERE ur.""IsDeleted"" = false
                    ORDER BY ur.""UserId"", r.""RoleCode"" DESC
                ) sub
                WHERE u.""Id"" = sub.""UserId"";

                UPDATE ""Users""
                SET ""RoleId"" = '11111111-1111-1111-1111-111111111111'
                WHERE ""RoleId"" = '00000000-0000-0000-0000-000000000000' OR ""RoleId"" IS NULL;
            ");

            migrationBuilder.UpdateData(
                table: "UserPenalties",
                keyColumn: "Id",
                keyValue: new Guid("60000000-aaaa-bbbb-cccc-000000000001"),
                column: "EvidenceUrls",
                value: new List<string>());

            migrationBuilder.UpdateData(
                table: "UserReports",
                keyColumn: "Id",
                keyValue: new Guid("50000000-aaaa-bbbb-cccc-000000000001"),
                column: "EvidenceUrls",
                value: new List<string> { "https://images.nexus.local/reports/invoice-request.png" });

            migrationBuilder.UpdateData(
                table: "UserReviews",
                keyColumn: "Id",
                keyValue: new Guid("40000000-aaaa-bbbb-cccc-000000000001"),
                column: "MediaUrls",
                value: new List<string> { "https://images.nexus.local/reviews/user-review-01.png" });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("aaaaaaaa-1111-1111-1111-111111111111"),
                column: "RoleId",
                value: new Guid("44444444-4444-4444-4444-444444444444"));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("bbbbbbbb-2222-2222-2222-222222222222"),
                column: "RoleId",
                value: new Guid("33333333-3333-3333-3333-333333333333"));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("cccccccc-3333-3333-3333-333333333333"),
                column: "RoleId",
                value: new Guid("22222222-2222-2222-2222-222222222222"));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("dddddddd-4444-4444-4444-444444444444"),
                column: "RoleId",
                value: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.CreateIndex(
                name: "IX_Users_RoleId",
                table: "Users",
                column: "RoleId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Roles_RoleId",
                table: "Users",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Roles_RoleId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_RoleId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RoleId",
                table: "Users");

            migrationBuilder.UpdateData(
                table: "UserPenalties",
                keyColumn: "Id",
                keyValue: new Guid("60000000-aaaa-bbbb-cccc-000000000001"),
                column: "EvidenceUrls",
                value: new List<string>());

            migrationBuilder.UpdateData(
                table: "UserReports",
                keyColumn: "Id",
                keyValue: new Guid("50000000-aaaa-bbbb-cccc-000000000001"),
                column: "EvidenceUrls",
                value: new List<string> { "https://images.nexus.local/reports/invoice-request.png" });

            migrationBuilder.UpdateData(
                table: "UserReviews",
                keyColumn: "Id",
                keyValue: new Guid("40000000-aaaa-bbbb-cccc-000000000001"),
                column: "MediaUrls",
                value: new List<string> { "https://images.nexus.local/reviews/user-review-01.png" });
        }
    }
}
