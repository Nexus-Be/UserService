using User.Domain.Base;
using User.Domain.Enums;

namespace User.Domain.Entities;

public class UserReport : BaseEntity<Guid>
{
    public required Guid ReporterId { get; set; }
    public required Guid ReportedUserId { get; set; }
    public TargetRole TargetRole { get; set; } = TargetRole.Account;
    public ReferenceType? ReferenceType { get; set; }
    public string? ReferenceId { get; set; }
    public required ReportViolationType ViolationType { get; set; }
    public required string Description { get; set; }
    public List<string> EvidenceUrls { get; set; } = new();
    public required ReportStatus Status { get; set; } = ReportStatus.Pending;
    public Guid? ResolvedBy { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public string? ResolutionNote { get; set; }

    /// <summary>Refund of the buyer after a dispute decided in their favour. None unless staff asked for a refund.</summary>
    public ReportRefundStatus RefundStatus { get; set; } = ReportRefundStatus.None;
    public decimal? RefundAmount { get; set; }
    public string? RefundMessage { get; set; }

    // Navigation
    public User? Reporter { get; set; }
    public User? ReportedUser { get; set; }
    public User? ResolvedByUser { get; set; }
}
