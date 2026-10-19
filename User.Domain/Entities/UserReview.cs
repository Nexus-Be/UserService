using User.Domain.Base;
using User.Domain.Enums;

namespace User.Domain.Entities;

public class UserReview : BaseEntity<Guid>
{
    public required Guid ReviewerId { get; set; }
    public required Guid RevieweeId { get; set; }
    public required ReviewerRole ReviewerRole { get; set; } = ReviewerRole.Buyer;
    public required ReferenceType ReferenceType { get; set; }
    public required string ReferenceId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public bool IsDisputed { get; set; }
    public bool IsDisabled { get; set; }

    // Navigation
    public User? Reviewer { get; set; }
    public User? Reviewee { get; set; }
}
