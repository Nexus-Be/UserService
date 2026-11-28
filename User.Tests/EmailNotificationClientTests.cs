namespace User.Tests;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using User.Application.Interfaces;
using User.Application.Models;
using User.Application.Services.Auth;
using User.Domain.Entities;
using User.Domain.Enums;
using User.Infrastructure.Persistence;
using User.Infrastructure.Services.Notification;
using Xunit;

public sealed class EmailNotificationClientTests
{
    private static ServiceProvider CreateServiceProvider(string dbName, HttpMessageHandler handler)
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(opt => opt.UseInMemoryDatabase(dbName));
        services.AddScoped<IUserDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddHttpClient(NotificationRetryWorker.HttpClientName, c =>
        {
            c.BaseAddress = new Uri("http://localhost:8007/");
        }).ConfigurePrimaryHttpMessageHandler(() => handler);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task SendPasswordChangedEmailAsync_Sends_Expected_Route_And_Payload()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"notificationId":"notif-123"}""", Encoding.UTF8, "application/json")
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8007/") };
        var client = new EmailNotificationClient(httpClient, NullLogger<EmailNotificationClient>.Instance);
        var userId = Guid.NewGuid();
        var changedAt = DateTimeOffset.UtcNow;

        var result = await client.SendPasswordChangedEmailAsync(
            "pwd-changed:1",
            userId,
            "user@example.com",
            "Nguyen Van A",
            PasswordChangeKind.Changed,
            changedAt,
            "trace-abc");

        Assert.True(result);
        Assert.Single(handler.Requests);
        Assert.Equal("http://localhost:8007/api/v1/notifications/password-changes", handler.Requests[0].Uri);

        using var doc = JsonDocument.Parse(handler.Requests[0].Body);
        var root = doc.RootElement;
        Assert.True(Guid.TryParse(root.GetProperty("sourceEventId").GetString(), out var parsedEventId));
        Assert.NotEqual(Guid.Empty, parsedEventId);
        Assert.Equal(userId.ToString(), root.GetProperty("userId").GetString());
        Assert.Equal("user@example.com", root.GetProperty("email").GetString());
        Assert.Equal("Changed", root.GetProperty("kind").GetString());
        Assert.Equal("Nguyen Van A", root.GetProperty("fullName").GetString());
        Assert.Equal("trace-abc", root.GetProperty("traceId").GetString());
    }

    [Fact]
    public async Task SendSellerDecisionEmailAsync_And_AccountStatusChanged_Use_Unified_Routes()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8007/") };
        var client = new EmailNotificationClient(httpClient, NullLogger<EmailNotificationClient>.Instance);

        var okSeller = await client.SendSellerDecisionEmailAsync(
            "seller-1",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "seller@example.com",
            "Shop Owner",
            "My Shop",
            isApproved: false,
            reason: "Missing tax ID",
            decidedAt: DateTimeOffset.UtcNow,
            traceId: "trace-s1");

        var okBan = await client.SendAccountStatusChangedEmailAsync(
            "ban-1",
            Guid.NewGuid(),
            "buyer@example.com",
            "Buyer",
            isBanned: true,
            reason: "Fraud",
            changedAt: DateTimeOffset.UtcNow,
            traceId: "trace-b1");

        Assert.True(okSeller);
        Assert.True(okBan);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("http://localhost:8007/api/v1/notifications/seller-decisions", handler.Requests[0].Uri);
        Assert.Equal("http://localhost:8007/api/v1/notifications/account-status-changes", handler.Requests[1].Uri);

        using var sellerDoc = JsonDocument.Parse(handler.Requests[0].Body);
        Assert.True(Guid.TryParse(sellerDoc.RootElement.GetProperty("sourceEventId").GetString(), out _));
        Assert.True(Guid.TryParse(sellerDoc.RootElement.GetProperty("applicationId").GetString(), out _));
        Assert.Equal("Rejected", sellerDoc.RootElement.GetProperty("decision").GetString());
        Assert.Equal("Missing tax ID", sellerDoc.RootElement.GetProperty("rejectionReason").GetString());

        using var banDoc = JsonDocument.Parse(handler.Requests[1].Body);
        Assert.True(Guid.TryParse(banDoc.RootElement.GetProperty("sourceEventId").GetString(), out _));
        Assert.True(banDoc.RootElement.GetProperty("statusVersion").GetInt64() >= 1);
        Assert.Equal("Banned", banDoc.RootElement.GetProperty("change").GetString());
        Assert.Equal("Fraud", banDoc.RootElement.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Failed_Dispatch_Persists_Log_And_Worker_Replays_Same_SourceEventId()
    {
        var dbName = $"NotifRetry_{Guid.NewGuid()}";
        var shouldFail = true;

        var handler = new RecordingHandler(_ =>
        {
            if (shouldFail)
            {
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                {
                    Content = new StringContent("Downstream unavailable", Encoding.UTF8, "text/plain")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"notificationId":"notif-replay-99"}""", Encoding.UTF8, "application/json")
            };
        });

        await using var sp = CreateServiceProvider(dbName, handler);
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8007/") };
        var client = new EmailNotificationClient(httpClient, NullLogger<EmailNotificationClient>.Instance, scopeFactory);

        var initialResult = await client.SendPasswordChangedEmailAsync(
            "pwd-reset:retry-1",
            Guid.NewGuid(),
            "retry@example.com",
            "Retry User",
            PasswordChangeKind.Reset,
            DateTimeOffset.UtcNow,
            "trace-r1");

        Assert.False(initialResult);

        await using (var verifyScope = scopeFactory.CreateAsyncScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var log = await db.NotificationDispatchLogs.SingleAsync();
            Assert.True(Guid.TryParse(log.SourceEventId, out var loggedEventId));
            Assert.NotEqual(Guid.Empty, loggedEventId);
            Assert.Equal(NotificationDispatchStatus.PendingRetry, log.Status);
            Assert.Equal(503, log.LastHttpStatusCode);

            // Make log immediately due for worker test
            log.NextRetryAt = DateTimeOffset.UtcNow.AddSeconds(-1);
            await db.SaveChangesAsync();
        }

        shouldFail = false;
        var worker = new NotificationRetryWorker(
            scopeFactory,
            sp.GetRequiredService<IHttpClientFactory>(),
            NullLogger<NotificationRetryWorker>.Instance);

        await worker.ProcessDueRetriesAsync(CancellationToken.None);

        await using (var verifyScope = scopeFactory.CreateAsyncScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var log = await db.NotificationDispatchLogs.SingleAsync();
            Assert.Equal(NotificationDispatchStatus.Sent, log.Status);
            Assert.Equal(200, log.LastHttpStatusCode);
            Assert.Equal("notif-replay-99", log.NotificationId);
            Assert.Equal(1, log.RetryCount);
        }

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(handler.Requests[0].Body, handler.Requests[1].Body);
    }

    [Fact]
    public async Task AuthService_ChangePassword_And_ResetPassword_Dispatch_PasswordChanged_Notification()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"AuthPwdNotif_{Guid.NewGuid()}")
            .Options;

        await using var db = new AppDbContext(options);
        var user = new global::User.Domain.Entities.User
        {
            Id = Guid.NewGuid(),
            Email = "pwdtest@example.com",
            FirstName = "Test",
            LastName = "User",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("OldPass123!"),
            Status = UserStatus.Active
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var fakeOtp = new StubOtpService();
        var fakeNotif = new StubNotificationClient();
        var authService = new AuthService(db, fakeOtp, NullLogger<AuthService>.Instance, emailNotificationClient: fakeNotif);

        var changeResult = await authService.ChangePasswordAsync(
            user.Id,
            new ChangePasswordDto("OldPass123!", "NewPass123!", "NewPass123!"));

        Assert.True(changeResult.Success);
        Assert.Single(fakeNotif.PasswordChanges);
        Assert.Equal(PasswordChangeKind.Changed, fakeNotif.PasswordChanges[0].Kind);
        Assert.Equal(user.Id, fakeNotif.PasswordChanges[0].UserId);

        fakeOtp.ActiveResetOtp = new OtpEntry
        {
            Code = "123456",
            Status = OtpStatus.Active,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5)
        };

        var resetResult = await authService.ResetPasswordAsync(
            new ResetPasswordDto("pwdtest@example.com", "123456", "ResetPass123!", "ResetPass123!"));

        Assert.True(resetResult.Success);
        Assert.Equal(2, fakeNotif.PasswordChanges.Count);
        Assert.Equal(PasswordChangeKind.Reset, fakeNotif.PasswordChanges[1].Kind);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public List<(string Uri, string Body)> Requests { get; } = new();

        public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content != null
                ? await request.Content.ReadAsStringAsync(cancellationToken)
                : string.Empty;
            Requests.Add((request.RequestUri!.ToString(), body));
            return _responder(request);
        }
    }

    private sealed class StubNotificationClient : IEmailNotificationClient
    {
        public List<(string SourceEventId, Guid UserId, string Email, string FullName, PasswordChangeKind Kind)> PasswordChanges { get; } = new();

        public Task<bool> SendOtpEmailAsync(string email, string fullName, string otpCode, int expiresInMinutes = 5, string? purpose = null, string? traceId = null, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> SendSellerDecisionEmailAsync(string sourceEventId, Guid registrationId, Guid userId, string email, string fullName, string shopName, bool isApproved, string? reason, DateTimeOffset decidedAt, string? traceId = null, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> SendAccountStatusChangedEmailAsync(string sourceEventId, Guid userId, string email, string fullName, bool isBanned, string? reason, DateTimeOffset changedAt, string? traceId = null, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<bool> SendPasswordChangedEmailAsync(string sourceEventId, Guid userId, string email, string fullName, PasswordChangeKind kind, DateTimeOffset changedAt, string? traceId = null, CancellationToken cancellationToken = default)
        {
            PasswordChanges.Add((sourceEventId, userId, email, fullName, kind));
            return Task.FromResult(true);
        }
    }

    private sealed class StubOtpService : IRedisOtpService
    {
        public OtpEntry? ActiveResetOtp { get; set; }

        public Task SaveOtpAsync(string email, string otpCode, TimeSpan validDuration, TimeSpan storageDuration, string purpose = "registration") => Task.CompletedTask;
        public Task<OtpEntry?> GetOtpAsync(string email, string purpose = "registration") => Task.FromResult(ActiveResetOtp);
        public Task MarkOtpConsumedAsync(string email, string purpose = "registration") => Task.CompletedTask;
        public Task InvalidateExistingOtpsAsync(string email, string purpose = "registration") => Task.CompletedTask;
        public Task<bool> IsCooldownActiveAsync(string email, string purpose = "registration") => Task.FromResult(false);
        public Task SetCooldownAsync(string email, TimeSpan duration, string purpose = "registration") => Task.CompletedTask;
        public Task<int> IncrementOtpAttemptsAsync(string email, TimeSpan window, string purpose = "registration") => Task.FromResult(1);
        public Task ResetOtpAttemptsAsync(string email, string purpose = "registration") => Task.CompletedTask;
        public Task<int> IncrementLoginAttemptsAsync(string identifier, TimeSpan window) => Task.FromResult(1);
        public Task<bool> IsAccountLockedAsync(string identifier) => Task.FromResult(false);
        public Task LockAccountAsync(string identifier, TimeSpan duration) => Task.CompletedTask;
        public Task ResetLoginAttemptsAsync(string identifier) => Task.CompletedTask;
    }
}
