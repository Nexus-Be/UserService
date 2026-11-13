using System;
using User.Domain.Base;
using User.Domain.Enums;

namespace User.Domain.Entities;

public class UserAuctionProfile : AuditableEntity<Guid>
{
    public required Guid UserId { get; set; }
    public int ViolationStreak { get; set; }
    public AuctionRestrictionType RestrictionType { get; set; } = AuctionRestrictionType.None;
    public DateTimeOffset? RestrictionUntil { get; set; }
    public string? RestrictionReason { get; set; }

    public User? User { get; set; }

    public bool IsCurrentlyRestricted(DateTimeOffset now)
    {
        if (RestrictionType == AuctionRestrictionType.None)
        {
            return false;
        }

        if (RestrictionUntil.HasValue && RestrictionUntil.Value <= now)
        {
            return false;
        }

        return true;
    }
}
