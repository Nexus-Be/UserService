namespace User.Infrastructure.Services.Notification;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using User.Application.Interfaces;

public sealed class QueuedEmailNotificationClient : IEmailNotificationClient
{
    private readonly OtpEmailQueue _queue;
    private readonly ILogger<QueuedEmailNotificationClient> _logger;

    public QueuedEmailNotificationClient(OtpEmailQueue queue, ILogger<QueuedEmailNotificationClient> logger)
    {
        _queue = queue;
        _logger = logger;
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
}
