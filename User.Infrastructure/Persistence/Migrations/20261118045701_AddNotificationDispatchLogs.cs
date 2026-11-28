using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace User.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationDispatchLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotificationDispatchLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceEventId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    TraceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Endpoint = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    MaxRetries = table.Column<int>(type: "integer", nullable: false),
                    LastHttpStatusCode = table.Column<int>(type: "integer", nullable: true),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    NotificationId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    LastAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NextRetryAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationDispatchLogs", x => x.Id);
                });

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
                name: "IX_NotificationDispatchLogs_SourceEventId",
                table: "NotificationDispatchLogs",
                column: "SourceEventId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDispatchLogs_Status_NextRetryAt",
                table: "NotificationDispatchLogs",
                columns: new[] { "Status", "NextRetryAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationDispatchLogs");

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
