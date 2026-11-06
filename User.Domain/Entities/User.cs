using System;
using System.Collections.Generic;
using System.Linq;
using User.Domain.Base;
using User.Domain.Enums;

namespace User.Domain.Entities;

public class User : AuditableEntity<Guid>
{
    public string? Username { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? IdentifyNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public Gender Gender { get; set; } = Gender.Unspecified;
    public DateTimeOffset? DateOfBirth { get; set; }
    public string? Address { get; set; }
    public required UserStatus Status { get; set; } = UserStatus.Active;
    public DateTimeOffset? EmailVerifiedAt { get; set; }
    public DateTimeOffset? BannedAt { get; set; }
    public Guid? BannedBy { get; set; }
    public string? BanReason { get; set; }

    // 1 User 1 Role core assignment (Default: Buyer)
    public Guid RoleId { get; set; }

    // Navigation
    public Role? Role { get; set; }
    public User? BannedByUser { get; set; }
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    public SellerProfile? SellerProfile { get; set; }
    public ReputationScore? ReputationScore { get; set; }
    public ICollection<ReputationLog> ReputationLogs { get; set; } = new List<ReputationLog>();
    public ICollection<UserReview> ReviewsGiven { get; set; } = new List<UserReview>();
    public ICollection<UserReview> ReviewsReceived { get; set; } = new List<UserReview>();
    public ICollection<UserReport> ReportsSubmitted { get; set; } = new List<UserReport>();
    public ICollection<UserReport> ReportsReceived { get; set; } = new List<UserReport>();
    public ICollection<UserReport> ReportsResolved { get; set; } = new List<UserReport>();
    public ICollection<UserPenalty> PenaltiesReceived { get; set; } = new List<UserPenalty>();
    public ICollection<UserPenalty> PenaltiesIssued { get; set; } = new List<UserPenalty>();
    public ICollection<UserPenalty> PenaltiesRevoked { get; set; } = new List<UserPenalty>();

    public void ApplyAccountBan(string reason, Guid? adminId)
    {
        Status = UserStatus.Banned;
        BannedAt = DateTimeOffset.UtcNow;
        BannedBy = adminId;
        BanReason = reason;
        UpdatedAt = DateTimeOffset.UtcNow;
        if (adminId.HasValue)
        {
            UpdatedBy = adminId.Value;
        }
    }

    public bool CanPlaceOrder()
    {
        if (Status != UserStatus.Active || IsDeleted)
        {
            return false;
        }

        var hasActiveBuyerRestriction = PenaltiesReceived.Any(p =>
            !p.IsDeleted &&
            p.IsCurrentlyActive() &&
            (p.TargetRole == TargetRole.Buyer || p.TargetRole == TargetRole.Account) &&
            (p.PenaltyApplied == PenaltyType.Restriction || p.PenaltyApplied == PenaltyType.BanAccount));

        return !hasActiveBuyerRestriction;
    }

    public bool CanSell()
    {
        if (Status != UserStatus.Active || IsDeleted)
        {
            return false;
        }

        if (SellerProfile == null || SellerProfile.IsDeleted || SellerProfile.Status != SellerProfileStatus.Approved)
        {
            return false;
        }

        var hasActiveSellerSanction = PenaltiesReceived.Any(p =>
            !p.IsDeleted &&
            p.IsCurrentlyActive() &&
            (p.TargetRole == TargetRole.Seller || p.TargetRole == TargetRole.Account) &&
            (p.PenaltyApplied == PenaltyType.Restriction || p.PenaltyApplied == PenaltyType.BanAccount));

        return !hasActiveSellerSanction;
    }
}
