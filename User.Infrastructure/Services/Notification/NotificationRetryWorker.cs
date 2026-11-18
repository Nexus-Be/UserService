namespace User.Infrastructure.Services.Notification;

using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using User.Application.Interfaces;
using User.Domain.Enums;

/// <summary>
/// Background worker that replays failed outbound NotificationService REST requests
/// using the exact persisted SourceEventId and JSON payload.
/// </summary>
public sealed class NotificationRetryWorker : BackgroundService
{
    public const string HttpClientName = "NotificationRetry";
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan[] RetryBackoffSchedule =
    [
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromMinutes(60)
    ];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<NotificationRetryWorker> _logger;

    public NotificationRetryWorker(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        ILogger<NotificationRetryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueRetriesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[NotificationRetryWorker] Unexpected error while polling failed notifications.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    public async Task ProcessDueRetriesAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IUserDbContext>();
        var now = DateTimeOffset.UtcNow;

        var dueItems = await dbContext.NotificationDispatchLogs
            .Where(x => x.Status == NotificationDispatchStatus.PendingRetry
                && (x.NextRetryAt == null || x.NextRetryAt <= now))
            .OrderBy(x => x.NextRetryAt)
            .Take(20)
            .ToListAsync(cancellationToken);

        if (dueItems.Count == 0)
        {
            return;
        }

        var client = _httpClientFactory.CreateClient(HttpClientName);

        foreach (var item in dueItems)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            item.LastAttemptAt = DateTimeOffset.UtcNow;
            item.RetryCount += 1;

            try
            {
                using var content = new StringContent(item.PayloadJson, Encoding.UTF8, "application/json");
                using var response = await client.PostAsync(item.Endpoint, content, cancellationToken);
                item.LastHttpStatusCode = (int)response.StatusCode;
                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    item.Status = NotificationDispatchStatus.Sent;
                    item.SentAt = DateTimeOffset.UtcNow;
                    item.NextRetryAt = null;
                    item.LastError = null;
                    item.NotificationId = EmailNotificationClient.TryExtractNotificationId(body);

                    _logger.LogInformation(
                        "[NotificationRetryWorker] Replay succeeded for SourceEventId '{SourceEventId}' (Endpoint: {Endpoint}, NotificationId: {NotificationId}).",
                        item.SourceEventId, item.Endpoint, item.NotificationId ?? "N/A");
                    continue;
                }

                item.LastError = EmailNotificationClient.Truncate(body, 1900);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                item.LastError = EmailNotificationClient.Truncate(ex.Message, 1900);
            }

            if (item.RetryCount >= item.MaxRetries)
            {
                item.Status = NotificationDispatchStatus.Exhausted;
                item.NextRetryAt = null;
                _logger.LogError(
                    "[NotificationRetryWorker] Exhausted {MaxRetries} retries for SourceEventId '{SourceEventId}' (Endpoint: {Endpoint}).",
                    item.MaxRetries, item.SourceEventId, item.Endpoint);
            }
            else
            {
                var delayIndex = Math.Clamp(item.RetryCount - 1, 0, RetryBackoffSchedule.Length - 1);
                item.NextRetryAt = DateTimeOffset.UtcNow.Add(RetryBackoffSchedule[delayIndex]);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
