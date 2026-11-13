using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace User.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingReputationLogReviewAndReportColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. ReputationLogs columns & indexes
            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "ReputationLogs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PartnerUserId",
                table: "ReputationLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScoreAfter",
                table: "ReputationLogs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TargetRole",
                table: "ReputationLogs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.DropIndex(
                name: "IX_ReputationLogs_UserId",
                table: "ReputationLogs");

            migrationBuilder.CreateIndex(
                name: "IX_ReputationLogs_UserId_TargetRole_PartnerUserId_CreatedAt",
                table: "ReputationLogs",
                columns: new[] { "UserId", "TargetRole", "PartnerUserId", "CreatedAt" });

            // 2. UserReports columns
            migrationBuilder.AddColumn<string>(
                name: "ReferenceId",
                table: "UserReports",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReferenceType",
                table: "UserReports",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ResolvedAt",
                table: "UserReports",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TargetRole",
                table: "UserReports",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            // 3. UserReviews columns & indexes
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "HiddenAt",
                table: "UserReviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "HiddenBy",
                table: "UserReviews",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HiddenReason",
                table: "UserReviews",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScoreImpact",
                table: "UserReviews",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.DropIndex(
                name: "IX_UserReviews_ReviewerId",
                table: "UserReviews");

            migrationBuilder.CreateIndex(
                name: "IX_UserReviews_ReviewerId_ReferenceType_ReferenceId",
                table: "UserReviews",
                columns: new[] { "ReviewerId", "ReferenceType", "ReferenceId" },
                unique: true);

            // 4. Update seed data for ReputationLogs
            migrationBuilder.UpdateData(
                table: "ReputationLogs",
                keyColumn: "Id",
                keyValue: new Guid("30000000-aaaa-bbbb-cccc-000000000001"),
                columns: new[] { "ChangeAmount", "PartnerUserId", "ScoreAfter", "TargetRole" },
                values: new object[] { 35, new Guid("dddddddd-4444-4444-4444-444444444444"), 85, 1 });

            migrationBuilder.UpdateData(
                table: "ReputationLogs",
                keyColumn: "Id",
                keyValue: new Guid("30000000-aaaa-bbbb-cccc-000000000002"),
                columns: new[] { "ChangeAmount", "PartnerUserId", "ScoreAfter", "TargetRole" },
                values: new object[] { 10, new Guid("cccccccc-3333-3333-3333-333333333333"), 80, 0 });

            // 5. Update seed data for UserReports
            migrationBuilder.UpdateData(
                table: "UserReports",
                keyColumn: "Id",
                keyValue: new Guid("50000000-aaaa-bbbb-cccc-000000000001"),
                columns: new[] { "ResolvedAt", "TargetRole" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1 });

            // 6. Update seed data for UserReviews
            migrationBuilder.UpdateData(
                table: "UserReviews",
                keyColumn: "Id",
                keyValue: new Guid("40000000-aaaa-bbbb-cccc-000000000001"),
                column: "ScoreImpact",
                value: 1);

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
            migrationBuilder.DropIndex(
                name: "IX_UserReviews_ReviewerId_ReferenceType_ReferenceId",
                table: "UserReviews");

            migrationBuilder.CreateIndex(
                name: "IX_UserReviews_ReviewerId",
                table: "UserReviews",
                column: "ReviewerId");

            migrationBuilder.DropColumn(
                name: "HiddenAt",
                table: "UserReviews");

            migrationBuilder.DropColumn(
                name: "HiddenBy",
                table: "UserReviews");

            migrationBuilder.DropColumn(
                name: "HiddenReason",
                table: "UserReviews");

            migrationBuilder.DropColumn(
                name: "ScoreImpact",
                table: "UserReviews");

            migrationBuilder.DropColumn(
                name: "ReferenceId",
                table: "UserReports");

            migrationBuilder.DropColumn(
                name: "ReferenceType",
                table: "UserReports");

            migrationBuilder.DropColumn(
                name: "ResolvedAt",
                table: "UserReports");

            migrationBuilder.DropColumn(
                name: "TargetRole",
                table: "UserReports");

            migrationBuilder.DropIndex(
                name: "IX_ReputationLogs_UserId_TargetRole_PartnerUserId_CreatedAt",
                table: "ReputationLogs");

            migrationBuilder.CreateIndex(
                name: "IX_ReputationLogs_UserId",
                table: "ReputationLogs",
                column: "UserId");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "ReputationLogs");

            migrationBuilder.DropColumn(
                name: "PartnerUserId",
                table: "ReputationLogs");

            migrationBuilder.DropColumn(
                name: "ScoreAfter",
                table: "ReputationLogs");

            migrationBuilder.DropColumn(
                name: "TargetRole",
                table: "ReputationLogs");
        }
    }
}
