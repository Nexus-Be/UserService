namespace User.Infrastructure.BackgroundWorkers;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using User.Infrastructure.Services.Notification;

public sealed class OtpEmailDispatchWorker : BackgroundService
{
    private readonly OtpEmailQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OtpEmailDispatchWorker> _logger;

    public OtpEmailDispatchWorker(
        OtpEmailQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<OtpEmailDispatchWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OtpEmailDispatchWorker is started and listening for OTP email dispatches.");

        while (!stoppingToken.IsCancellationRequested)
        {
            OtpEmailMessage message;
            try
            {
                message = await _queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var client = scope.ServiceProvider.GetRequiredService<EmailNotificationClient>();
                var sent = await client.SendOtpEmailAsync(
                    message.Email,
                    message.FullName,
                    message.OtpCode,
                    message.ExpiresInMinutes,
                    stoppingToken);

                if (!sent)
                {
                    _logger.LogWarning("Failed to dispatch OTP email to '{Email}' from background worker.", message.Email);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OTP email background dispatch failed for '{Email}'.", message.Email);
            }
        }

        _logger.LogInformation("OtpEmailDispatchWorker is shutting down.");
    }
}
