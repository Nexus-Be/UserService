using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace User.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFirebaseGoogleLoginSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                table: "Users",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AddColumn<string>(
                name: "FirebaseUid",
                table: "Users",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RefundAmount",
                table: "UserReports",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefundMessage",
                table: "UserReports",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RefundStatus",
                table: "UserReports",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxValue",
                table: "SystemConfigs",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinValue",
                table: "SystemConfigs",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WarehouseReady",
                table: "SellerProfiles",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.UpdateData(
                table: "SellerProfiles",
                keyColumn: "Id",
                keyValue: new Guid("cccccccc-3333-3333-3333-333333333334"),
                column: "WarehouseReady",
                value: true);

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Id",
                keyValue: new Guid("11111111-2222-3333-4444-555555555551"),
                columns: new[] { "MaxValue", "MinValue" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Id",
                keyValue: new Guid("11111111-2222-3333-4444-555555555552"),
                columns: new[] { "MaxValue", "MinValue" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Id",
                keyValue: new Guid("11111111-2222-3333-4444-555555555553"),
                columns: new[] { "MaxValue", "MinValue" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Id",
                keyValue: new Guid("11111111-2222-3333-4444-555555555554"),
                columns: new[] { "MaxValue", "MinValue" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Id",
                keyValue: new Guid("11111111-2222-3333-4444-555555555555"),
                columns: new[] { "MaxValue", "MinValue" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Id",
                keyValue: new Guid("11111111-2222-3333-4444-555555555556"),
                columns: new[] { "MaxValue", "MinValue" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Id",
                keyValue: new Guid("11111111-2222-3333-4444-555555555557"),
                columns: new[] { "MaxValue", "MinValue" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Id",
                keyValue: new Guid("11111111-2222-3333-4444-555555555558"),
                columns: new[] { "MaxValue", "MinValue" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "SystemConfigs",
                keyColumn: "Id",
                keyValue: new Guid("11111111-2222-3333-4444-555555555559"),
                columns: new[] { "MaxValue", "MinValue" },
                values: new object[] { null, null });

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
                columns: new[] { "EvidenceUrls", "RefundAmount", "RefundMessage", "RefundStatus" },
                values: new object[] { new List<string> { "https://images.nexus.local/reports/invoice-request.png" }, null, null, 0 });

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
                column: "FirebaseUid",
                value: null);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("bbbbbbbb-2222-2222-2222-222222222222"),
                column: "FirebaseUid",
                value: null);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("cccccccc-3333-3333-3333-333333333333"),
                column: "FirebaseUid",
                value: null);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("dddddddd-4444-4444-4444-444444444444"),
                column: "FirebaseUid",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_Users_FirebaseUid",
                table: "Users",
                column: "FirebaseUid",
                unique: true,
                filter: "\"FirebaseUid\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserReports_RefundStatus",
                table: "UserReports",
                column: "RefundStatus");

            migrationBuilder.CreateIndex(
                name: "IX_ReputationLogs_Idempotency",
                table: "ReputationLogs",
                columns: new[] { "UserId", "TargetRole", "ReferenceType", "ReferenceId", "Reason" },
                unique: true,
                filter: "\"ReferenceId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_FirebaseUid",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_UserReports_RefundStatus",
                table: "UserReports");

            migrationBuilder.DropIndex(
                name: "IX_ReputationLogs_Idempotency",
                table: "ReputationLogs");

            migrationBuilder.DropColumn(
                name: "FirebaseUid",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RefundAmount",
                table: "UserReports");

            migrationBuilder.DropColumn(
                name: "RefundMessage",
                table: "UserReports");

            migrationBuilder.DropColumn(
                name: "RefundStatus",
                table: "UserReports");

            migrationBuilder.DropColumn(
                name: "MaxValue",
                table: "SystemConfigs");

            migrationBuilder.DropColumn(
                name: "MinValue",
                table: "SystemConfigs");

            migrationBuilder.DropColumn(
                name: "WarehouseReady",
                table: "SellerProfiles");

            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                table: "Users",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

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
