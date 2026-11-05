using System;
using System.Collections.Generic;
using User.Domain.Base;
using User.Domain.Enums;

namespace User.Domain.Entities;

public class UserPenalty : BaseEntity<Guid>
{
    public required Guid UserId { get; set; }
    public required TargetRole TargetRole { get; set; } = TargetRole.Account;
    public required PenaltyViolationType ViolationType { get; set; }
    public required string Reason { get; set; }
    public required PenaltyType PenaltyApplied { get; set; }
    public int PointsDeducted { get; set; }
    public required PenaltyStatus Status { get; set; } = PenaltyStatus.Active;
    public DateTimeOffset IssuedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpiresAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? ReportId { get; set; }
    public string? PolicyClauseCode { get; set; }
    public List<string> EvidenceUrls { get; set; } = new();
    public Guid? RevokedBy { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? RevokeReason { get; set; }

    // Navigation
    public User? User { get; set; }
    public User? CreatedByUser { get; set; }
    public User? RevokedByUser { get; set; }
    public UserReport? Report { get; set; }

    public bool IsCurrentlyActive()
    {
        return Status == PenaltyStatus.Active &&
               (ExpiresAt == null || ExpiresAt.Value > DateTimeOffset.UtcNow);
    }

    public void Revoke(Guid adminId, string reason)
    {
        Status = PenaltyStatus.Revoked;
        RevokedBy = adminId;
        RevokedAt = DateTimeOffset.UtcNow;
        RevokeReason = reason;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
