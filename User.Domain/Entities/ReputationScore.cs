using User.Domain.Base;
using User.Domain.Enums;

namespace User.Domain.Entities;

public class ReputationScore : BaseEntity<Guid>
{
    public required Guid UserId { get; set; }
    public int CurrentScore { get; set; } = 50;
    public TrustLevel TrustLevel { get; set; } = TrustLevel.Standard;
    public int TotalPositiveReviews { get; set; }
    public int TotalNegativeReviews { get; set; }
    public int PenaltyCount { get; set; }

    // Navigation
    public User? User { get; set; }
}
