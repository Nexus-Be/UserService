namespace User.Infrastructure.Services.Notification;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using User.Application.Interfaces;

public sealed class QueuedEmailNotificationClient : IEmailNotificationClient
{
    private readonly OtpEmailQueue _queue;
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly ILogger<QueuedEmailNotificationClient> _logger;

    public QueuedEmailNotificationClient(
        OtpEmailQueue queue,
        ILogger<QueuedEmailNotificationClient> logger,
        IServiceScopeFactory? scopeFactory = null)
    {
        _queue = queue;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public Task<bool> SendOtpEmailAsync(
        string email,
        string fullName,
        string otpCode,
        int expiresInMinutes = 5,
        CancellationToken cancellationToken = default)
    {
        var message = new OtpEmailMessage(email, fullName, otpCode, expiresInMinutes);
        var enqueued = _queue.TryEnqueue(message);

        if (!enqueued)
        {
            _logger.LogWarning("OTP email queue is full. Dispatch skipped for '{Email}'.", email);
        }

        return Task.FromResult(enqueued);
    }

    public async Task<bool> SendSellerApprovedEmailAsync(
        string email,
        string fullName,
        string shopName,
        CancellationToken cancellationToken = default)
    {
        if (_scopeFactory == null)
        {
            _logger.LogInformation("[QueuedEmailNotificationClient] Dispatch skipped because scope factory is null (Test mode).");
            return true;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<EmailNotificationClient>();
            return await client.SendSellerApprovedEmailAsync(email, fullName, shopName, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to dispatch seller approval email to '{Email}'.", email);
            return false;
        }
    }

    public async Task<bool> SendSellerRejectedEmailAsync(
        string email,
        string fullName,
        string shopName,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (_scopeFactory == null)
        {
            _logger.LogInformation("[QueuedEmailNotificationClient] Dispatch skipped because scope factory is null (Test mode).");
            return true;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<EmailNotificationClient>();
            return await client.SendSellerRejectedEmailAsync(email, fullName, shopName, reason, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to dispatch seller rejection email to '{Email}'.", email);
            return false;
        }
    }

    public async Task<bool> SendUserBannedEmailAsync(
        string email,
        string fullName,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (_scopeFactory == null)
        {
            _logger.LogInformation("[QueuedEmailNotificationClient] Dispatch skipped because scope factory is null (Test mode).");
            return true;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<EmailNotificationClient>();
            return await client.SendUserBannedEmailAsync(email, fullName, reason, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to dispatch user banned email to '{Email}'.", email);
            return false;
        }
    }

    public async Task<bool> SendUserUnbannedEmailAsync(
        string email,
        string fullName,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        if (_scopeFactory == null)
        {
            _logger.LogInformation("[QueuedEmailNotificationClient] Dispatch skipped because scope factory is null (Test mode).");
            return true;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<EmailNotificationClient>();
            return await client.SendUserUnbannedEmailAsync(email, fullName, reason, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to dispatch user unbanned email to '{Email}'.", email);
            return false;
        }
    }
}
