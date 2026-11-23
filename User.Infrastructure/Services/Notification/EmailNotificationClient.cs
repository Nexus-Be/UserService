namespace User.Infrastructure.Services.Notification;

using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using User.Application.Interfaces;
using User.Domain.Entities;
using User.Domain.Enums;

/// <summary>
/// Outbound typed REST client for NotificationService with idempotent sourceEventId tracking
/// and DB-persisted retry logging for non-OTP business events.
/// </summary>
public sealed class EmailNotificationClient : IEmailNotificationClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ILogger<EmailNotificationClient> _logger;
    private readonly IServiceScopeFactory? _scopeFactory;

    public EmailNotificationClient(
        HttpClient httpClient,
        ILogger<EmailNotificationClient> logger,
        IServiceScopeFactory? scopeFactory = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public Task<bool> SendOtpEmailAsync(
        string email,
        string fullName,
        string otpCode,
        int expiresInMinutes = 5,
        string? purpose = null,
        string? traceId = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedTraceId = traceId ?? Activity.Current?.Id;
        var sourceEventId = $"otp:{Guid.NewGuid():N}";
        var payload = new
        {
            email = email.Trim(),
            fullName = fullName.Trim(),
            otpCode = otpCode.Trim(),
            expiresInMinutes,
            purpose = string.IsNullOrWhiteSpace(purpose) ? null : purpose.Trim(),
            traceId = resolvedTraceId
        };

        // OTPs are short-lived; do not enqueue delayed background retries.
        return PostNotificationAsync(
            "api/v1/notifications/email/otp",
            sourceEventId,
            resolvedTraceId,
            payload,
            persistRetryOnFailure: false,
            cancellationToken);
    }

    public Task<bool> SendSellerDecisionEmailAsync(
        string sourceEventId,
        Guid registrationId,
        Guid userId,
        string email,
        string fullName,
        string shopName,
        bool isApproved,
        string? reason,
        DateTimeOffset decidedAt,
        string? traceId = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedTraceId = traceId ?? Activity.Current?.Id;
        var trimmedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        var payload = new
        {
            sourceEventId,
            registrationId,
            userId,
            email = email.Trim(),
            fullName = fullName.Trim(),
            shopName = shopName.Trim(),
            decision = isApproved ? "Approved" : "Rejected",
            reason = trimmedReason,
            rejectReason = isApproved ? null : trimmedReason,
            decidedAt,
            traceId = resolvedTraceId
        };

        return PostNotificationAsync(
            "api/v1/notifications/seller-decisions",
            sourceEventId,
            resolvedTraceId,
            payload,
            persistRetryOnFailure: true,
            cancellationToken);
    }

    public Task<bool> SendAccountStatusChangedEmailAsync(
        string sourceEventId,
        Guid userId,
        string email,
        string fullName,
        bool isBanned,
        string? reason,
        DateTimeOffset changedAt,
        string? traceId = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedTraceId = traceId ?? Activity.Current?.Id;
        var payload = new
        {
            sourceEventId,
            userId,
            email = email.Trim(),
            fullName = fullName.Trim(),
            status = isBanned ? "Banned" : "Unbanned",
            action = isBanned ? "Ban" : "Unban",
            reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            changedAt,
            traceId = resolvedTraceId
        };

        return PostNotificationAsync(
            "api/v1/notifications/account-status-changes",
            sourceEventId,
            resolvedTraceId,
            payload,
            persistRetryOnFailure: true,
            cancellationToken);
    }

    public Task<bool> SendPasswordChangedEmailAsync(
        string sourceEventId,
        Guid userId,
        string email,
        string fullName,
        PasswordChangeKind kind,
        DateTimeOffset changedAt,
        string? traceId = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedTraceId = traceId ?? Activity.Current?.Id;
        var payload = new
        {
            sourceEventId,
            userId,
            email = email.Trim(),
            kind = kind.ToString(),
            changedAt,
            fullName = fullName.Trim(),
            traceId = resolvedTraceId
        };

        return PostNotificationAsync(
            "api/v1/notifications/password-changes",
            sourceEventId,
            resolvedTraceId,
            payload,
            persistRetryOnFailure: true,
            cancellationToken);
    }

    private async Task<bool> PostNotificationAsync(
        string endpoint,
        string sourceEventId,
        string? traceId,
        object payload,
        bool persistRetryOnFailure,
        CancellationToken cancellationToken)
    {
        var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
        int? statusCode = null;
        string? errorDetails = null;

        try
        {
            using var content = new StringContent(payloadJson, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);
            statusCode = (int)response.StatusCode;
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var notificationId = TryExtractNotificationId(responseBody);
                _logger.LogInformation(
                    "[REST Email Notification] Dispatched '{Endpoint}' (SourceEventId: {SourceEventId}, TraceId: {TraceId}, Status: {StatusCode}, NotificationId: {NotificationId}).",
                    endpoint, sourceEventId, traceId, statusCode, notificationId ?? "N/A");
                return true;
            }

            errorDetails = Truncate(responseBody, 1900);
            _logger.LogWarning(
                "[REST Email Notification] Failed '{Endpoint}' (SourceEventId: {SourceEventId}, TraceId: {TraceId}, Status: {StatusCode}, Details: {Details}).",
                endpoint, sourceEventId, traceId, statusCode, errorDetails);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            errorDetails = Truncate(ex.Message, 1900);
            _logger.LogWarning(
                ex,
                "[REST Email Notification] Connection error calling '{Endpoint}' (SourceEventId: {SourceEventId}, TraceId: {TraceId}).",
                endpoint, sourceEventId, traceId);
        }

        if (persistRetryOnFailure)
        {
            await PersistFailedDispatchAsync(endpoint, sourceEventId, traceId, payloadJson, statusCode, errorDetails);
        }

        return false;
    }

    private async Task PersistFailedDispatchAsync(
        string endpoint,
        string sourceEventId,
        string? traceId,
        string payloadJson,
        int? statusCode,
        string? errorDetails)
    {
        if (_scopeFactory == null)
        {
            _logger.LogWarning(
                "[REST Email Notification] IServiceScopeFactory is not configured; cannot persist NotificationDispatchLog for SourceEventId '{SourceEventId}'.",
                sourceEventId);
            return;
        }

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetService<IUserDbContext>();
            if (dbContext == null)
            {
                return;
            }

            var now = DateTimeOffset.UtcNow;
            dbContext.NotificationDispatchLogs.Add(new NotificationDispatchLog
            {
                Id = Guid.NewGuid(),
                SourceEventId = sourceEventId,
                TraceId = traceId,
                Endpoint = endpoint,
                PayloadJson = payloadJson,
                Status = NotificationDispatchStatus.PendingRetry,
                RetryCount = 0,
                MaxRetries = 5,
                LastHttpStatusCode = statusCode,
                LastError = errorDetails,
                CreatedAt = now,
                LastAttemptAt = now,
                NextRetryAt = now.AddSeconds(30)
            });

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[REST Email Notification] Failed to persist NotificationDispatchLog for SourceEventId '{SourceEventId}'.",
                sourceEventId);
        }
    }

    internal static string? TryExtractNotificationId(string? responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (TryGetStringProperty(root, "notificationId", out var id) ||
                TryGetStringProperty(root, "id", out id))
            {
                return id;
            }

            if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object &&
                (TryGetStringProperty(data, "notificationId", out id) || TryGetStringProperty(data, "id", out id)))
            {
                return id;
            }
        }
        catch (JsonException)
        {
            // Non-JSON response body is safe to ignore.
        }

        return null;
    }

    private static bool TryGetStringProperty(JsonElement obj, string propertyName, out string? value)
    {
        if (obj.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            value = prop.GetString();
            return !string.IsNullOrWhiteSpace(value);
        }

        value = null;
        return false;
    }

    internal static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
