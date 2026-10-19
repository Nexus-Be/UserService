using User.Domain.Base;
using User.Domain.Enums;

namespace User.Domain.Entities;

public class ReputationLog : BaseEntity<Guid>
{
    public required Guid UserId { get; set; }
    public int ChangeAmount { get; set; }
    public required ReputationChangeReason Reason { get; set; }
    public ReferenceType? ReferenceType { get; set; }
    public string? ReferenceId { get; set; }

    // Navigation
    public User? User { get; set; }
}
