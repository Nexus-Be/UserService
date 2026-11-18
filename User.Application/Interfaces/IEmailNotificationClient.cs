namespace User.Application.Interfaces;

using System;
using System.Threading;
using System.Threading.Tasks;
using User.Domain.Enums;

/// <summary>
/// Outbound typed REST client for NotificationService (port 8007).
/// </summary>
public interface IEmailNotificationClient
{
    Task<bool> SendOtpEmailAsync(
        string email,
        string fullName,
        string otpCode,
        int expiresInMinutes = 5,
        string? purpose = null,
        string? traceId = null,
        CancellationToken cancellationToken = default);

    Task<bool> SendSellerDecisionEmailAsync(
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
        CancellationToken cancellationToken = default);

    Task<bool> SendAccountStatusChangedEmailAsync(
        string sourceEventId,
        Guid userId,
        string email,
        string fullName,
        bool isBanned,
        string? reason,
        DateTimeOffset changedAt,
        string? traceId = null,
        CancellationToken cancellationToken = default);

    Task<bool> SendPasswordChangedEmailAsync(
        string sourceEventId,
        Guid userId,
        string email,
        string fullName,
        PasswordChangeKind kind,
        DateTimeOffset changedAt,
        string? traceId = null,
        CancellationToken cancellationToken = default);
}
