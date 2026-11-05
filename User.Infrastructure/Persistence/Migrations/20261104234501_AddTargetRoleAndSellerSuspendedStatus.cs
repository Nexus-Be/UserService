using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace User.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTargetRoleAndSellerSuspendedStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserPenalties_UserId",
                table: "UserPenalties");

            migrationBuilder.AddColumn<List<string>>(
                name: "EvidenceUrls",
                table: "UserPenalties",
                type: "text[]",
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "PolicyClauseCode",
                table: "UserPenalties",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReportId",
                table: "UserPenalties",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RevokeReason",
                table: "UserPenalties",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RevokedAt",
                table: "UserPenalties",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RevokedBy",
                table: "UserPenalties",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TargetRole",
                table: "UserPenalties",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.UpdateData(
                table: "UserPenalties",
                keyColumn: "Id",
                keyValue: new Guid("60000000-aaaa-bbbb-cccc-000000000001"),
                columns: new[] { "EvidenceUrls", "PolicyClauseCode", "ReportId", "RevokeReason", "RevokedAt", "RevokedBy" },
                values: new object[] { new List<string>(), null, null, null, null, null });

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
                name: "IX_UserPenalties_Active_Evaluation",
                table: "UserPenalties",
                columns: new[] { "UserId", "TargetRole", "PenaltyApplied", "ExpiresAt" },
                filter: "\"Status\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_UserPenalties_ReportId",
                table: "UserPenalties",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPenalties_RevokedBy",
                table: "UserPenalties",
                column: "RevokedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_UserPenalties_UserReports_ReportId",
                table: "UserPenalties",
                column: "ReportId",
                principalTable: "UserReports",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_UserPenalties_Users_RevokedBy",
                table: "UserPenalties",
                column: "RevokedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserPenalties_UserReports_ReportId",
                table: "UserPenalties");

            migrationBuilder.DropForeignKey(
                name: "FK_UserPenalties_Users_RevokedBy",
                table: "UserPenalties");

            migrationBuilder.DropIndex(
                name: "IX_UserPenalties_Active_Evaluation",
                table: "UserPenalties");

            migrationBuilder.DropIndex(
                name: "IX_UserPenalties_ReportId",
                table: "UserPenalties");

            migrationBuilder.DropIndex(
                name: "IX_UserPenalties_RevokedBy",
                table: "UserPenalties");

            migrationBuilder.DropColumn(
                name: "EvidenceUrls",
                table: "UserPenalties");

            migrationBuilder.DropColumn(
                name: "PolicyClauseCode",
                table: "UserPenalties");

            migrationBuilder.DropColumn(
                name: "ReportId",
                table: "UserPenalties");

            migrationBuilder.DropColumn(
                name: "RevokeReason",
                table: "UserPenalties");

            migrationBuilder.DropColumn(
                name: "RevokedAt",
                table: "UserPenalties");

            migrationBuilder.DropColumn(
                name: "RevokedBy",
                table: "UserPenalties");

            migrationBuilder.DropColumn(
                name: "TargetRole",
                table: "UserPenalties");

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
                name: "IX_UserPenalties_UserId",
                table: "UserPenalties",
                column: "UserId");
        }
    }
}
