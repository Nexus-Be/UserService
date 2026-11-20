namespace User.Domain.Enums;

/// <summary>Where the refund owed to the buyer after a dispute decided in their favour stands.</summary>
public enum ReportRefundStatus
{
    /// <summary>No refund was requested for this report.</summary>
    None = 0,
    /// <summary>A refund was requested but Commerce has not confirmed it yet; it can be retried.</summary>
    Pending = 1,
    /// <summary>Commerce confirmed that the buyer was refunded.</summary>
    Refunded = 2,
    /// <summary>The refund cannot be made automatically (for example the seller was already paid); staff must handle it.</summary>
    ManualProcessingRequired = 3,
    /// <summary>The refund failed and nothing was refunded; staff can retry it.</summary>
    Failed = 4
}
