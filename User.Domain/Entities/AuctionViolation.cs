using System;
using User.Domain.Base;
using User.Domain.Enums;

namespace User.Domain.Entities;

public class AuctionViolation : AuditableEntity<Guid>
{
    public required Guid UserId { get; set; }
    public AuctionViolationType? ViolationType { get; set; }
    public Guid? AuctionId { get; set; }
    public Guid? OrderId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public decimal DepositForfeitedAmount { get; set; }
    public AuctionViolationSource Source { get; set; }

    public int PreviousStreak { get; set; }
    public int NewStreak { get; set; }
    public int ReputationBefore { get; set; }
    public int ReputationAfter { get; set; }
    public AuctionSanctionType SanctionType { get; set; }
    public DateTimeOffset? SanctionUntil { get; set; }
    public string? Reason { get; set; }

    public User? User { get; set; }
}
