namespace User.Domain.Enums;

/// <summary>
/// Delivery state for outbound REST notifications recorded for retry.
/// </summary>
public enum NotificationDispatchStatus
{
    PendingRetry = 0,
    Sent = 1,
    Exhausted = 2
}
