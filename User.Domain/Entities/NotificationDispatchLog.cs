using User.Domain.Base;
using User.Domain.Enums;

namespace User.Domain.Entities;

/// <summary>
/// Persisted outbound notification dispatch record for idempotent REST retries.
/// </summary>
public class NotificationDispatchLog : BaseEntity<Guid>
{
    public NotificationDispatchLog()
    {
        Id = Guid.NewGuid();
    }

    public required string SourceEventId { get; set; }
    public string? TraceId { get; set; }
    public required string Endpoint { get; set; }
    public required string PayloadJson { get; set; }
    public NotificationDispatchStatus Status { get; set; } = NotificationDispatchStatus.PendingRetry;
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; } = 5;
    public int? LastHttpStatusCode { get; set; }
    public string? LastError { get; set; }
    public string? NotificationId { get; set; }
    public DateTimeOffset? LastAttemptAt { get; set; }
    public DateTimeOffset? NextRetryAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
}
