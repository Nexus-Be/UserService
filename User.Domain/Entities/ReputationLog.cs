using User.Domain.Base;
using User.Domain.Enums;

namespace User.Domain.Entities;

public class ReputationLog : BaseEntity<Guid>
{
    public required Guid UserId { get; set; }
    public TargetRole TargetRole { get; set; } = TargetRole.Buyer;
    public int ChangeAmount { get; set; }
    public int ScoreAfter { get; set; }
    public required ReputationChangeReason Reason { get; set; }
    public ReferenceType? ReferenceType { get; set; }
    public string? ReferenceId { get; set; }
    public Guid? PartnerUserId { get; set; }
    public string? Note { get; set; }
    public Guid? CreatedBy { get; set; }

    // Navigation
    public User? User { get; set; }
    public User? CreatedByUser { get; set; }
}
