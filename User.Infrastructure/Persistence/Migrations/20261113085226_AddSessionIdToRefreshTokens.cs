using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace User.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionIdToRefreshTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SessionId",
                table: "RefreshTokens",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql("UPDATE \"RefreshTokens\" SET \"SessionId\" = \"Id\" WHERE \"SessionId\" = '00000000-0000-0000-0000-000000000000';");

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

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_SessionId",
                table: "RefreshTokens",
                column: "SessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_SessionId",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "RefreshTokens");

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
