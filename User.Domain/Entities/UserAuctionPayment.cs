using User.Domain.Base;

namespace User.Domain.Entities;

public sealed class UserAuctionPayment : AuditableEntity<Guid>
{
    public required Guid UserId { get; set; }
    public required Guid AuctionId { get; set; }
    public required Guid OrderId { get; set; }
    public required string Source { get; set; }
    public required DateTimeOffset PaidAt { get; set; }
    public int PreviousStreak { get; set; }
    public bool RestrictionLifted { get; set; }
}
