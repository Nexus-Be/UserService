using User.Domain.Base;
using User.Domain.Enums;

namespace User.Domain.Entities;

public class UserPenalty : BaseEntity<Guid>
{
    public required Guid UserId { get; set; }
    public required PenaltyViolationType ViolationType { get; set; }
    public required string Reason { get; set; }
    public required PenaltyType PenaltyApplied { get; set; }
    public int PointsDeducted { get; set; }
    public required PenaltyStatus Status { get; set; } = PenaltyStatus.Active;
    public DateTimeOffset IssuedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpiresAt { get; set; }
    public Guid? CreatedBy { get; set; }

    // Navigation
    public User? User { get; set; }
    public User? CreatedByUser { get; set; }
}
