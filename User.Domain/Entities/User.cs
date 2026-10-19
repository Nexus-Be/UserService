using User.Domain.Base;
using User.Domain.Enums;

namespace User.Domain.Entities;

public class User : AuditableEntity<Guid>
{
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? IdentifyNumber { get; set; }
    public Gender Gender { get; set; } = Gender.Unspecified;
    public DateTimeOffset? DateOfBirth { get; set; }
    public string? Address { get; set; }
    public required UserStatus Status { get; set; } = UserStatus.Active;
    public DateTimeOffset? EmailVerifiedAt { get; set; }
    public DateTimeOffset? BannedAt { get; set; }
    public Guid? BannedBy { get; set; }
    public string? BanReason { get; set; }

    // Navigation
    public User? BannedByUser { get; set; }
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public SellerProfile? SellerProfile { get; set; }
    public ReputationScore? ReputationScore { get; set; }
    public ICollection<ReputationLog> ReputationLogs { get; set; } = new List<ReputationLog>();
    public ICollection<UserReview> ReviewsGiven { get; set; } = new List<UserReview>();
    public ICollection<UserReview> ReviewsReceived { get; set; } = new List<UserReview>();
}
