namespace User.Application.Services.Internal;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using User.Application.Interfaces;
using User.Domain.Enums;

public sealed class InternalUserService : IInternalUserService
{
    private readonly IUserDbContext _dbContext;
    private readonly ILogger<InternalUserService> _logger;

    public const int DefaultReputationScore = 50;
    public const int MinimumAllowedReputationScore = 50;

    public InternalUserService(
        IUserDbContext dbContext,
        ILogger<InternalUserService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CheckReputationResponse> CheckReputationAsync(
        CheckReputationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null || request.UserId == Guid.Empty)
        {
            return new CheckReputationResponse(
                UserId: request?.UserId.ToString() ?? string.Empty,
                Allowed: false,
                IsApprovedSeller: false,
                UserStatus: "Unknown",
                FailureReason: "ERR_USER_NOT_FOUND");
        }

        var user = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.SellerProfile)
            .Include(u => u.ReputationScore)
            .Include(u => u.PenaltiesReceived)
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user == null || user.IsDeleted || user.Status == UserStatus.Deleted)
        {
            _logger.LogInformation("[CheckReputation] User '{UserId}' not found or deleted.", request.UserId);
            return new CheckReputationResponse(
                UserId: request.UserId.ToString(),
                Allowed: false,
                IsApprovedSeller: false,
                UserStatus: "Unknown",
                FailureReason: "ERR_USER_NOT_FOUND");
        }

        var statusName = user.Status.ToString();

        if (user.Status == UserStatus.Banned)
        {
            _logger.LogInformation("[CheckReputation] User '{UserId}' is permanently banned.", request.UserId);
            return new CheckReputationResponse(
                UserId: request.UserId.ToString(),
                Allowed: false,
                IsApprovedSeller: false,
                UserStatus: statusName,
                FailureReason: "ERR_ACCOUNT_BANNED");
        }

        if (user.Status == UserStatus.PendingVerification)
        {
            _logger.LogInformation("[CheckReputation] User '{UserId}' is unverified.", request.UserId);
            return new CheckReputationResponse(
                UserId: request.UserId.ToString(),
                Allowed: false,
                IsApprovedSeller: false,
                UserStatus: statusName,
                FailureReason: "ERR_ACCOUNT_UNVERIFIED");
        }

        // Check active temporary sanctions / penalties
        var now = DateTimeOffset.UtcNow;
        var hasActiveTemporarySanction = user.PenaltiesReceived.Any(p =>
            p.IsCurrentlyActive() &&
            p.ExpiresAt != null &&
            (p.PenaltyApplied == PenaltyType.Restriction || p.PenaltyApplied == PenaltyType.BanAccount));

        if (hasActiveTemporarySanction)
        {
            _logger.LogInformation("[CheckReputation] User '{UserId}' has active temporary penalty.", request.UserId);
            return new CheckReputationResponse(
                UserId: request.UserId.ToString(),
                Allowed: false,
                IsApprovedSeller: false,
                UserStatus: statusName,
                FailureReason: "ERR_ACCOUNT_LOCKED_TEMPORARY");
        }

        // Check SellerProfile status
        var isApprovedSeller = user.SellerProfile != null && user.SellerProfile.Status == SellerProfileStatus.Approved;
        if (!isApprovedSeller)
        {
            _logger.LogInformation("[CheckReputation] User '{UserId}' is not an approved seller.", request.UserId);
            return new CheckReputationResponse(
                UserId: request.UserId.ToString(),
                Allowed: false,
                IsApprovedSeller: false,
                UserStatus: statusName,
                FailureReason: "ERR_ACCOUNT_UNVERIFIED");
        }

        // Check Reputation Score
        var currentScore = user.ReputationScore?.CurrentScore ?? DefaultReputationScore;
        if (currentScore < MinimumAllowedReputationScore)
        {
            _logger.LogInformation("[CheckReputation] User '{UserId}' reputation score ({Score}) is below required minimum ({Min}).",
                request.UserId, currentScore, MinimumAllowedReputationScore);
            return new CheckReputationResponse(
                UserId: request.UserId.ToString(),
                Allowed: false,
                IsApprovedSeller: false,
                UserStatus: statusName,
                FailureReason: "ERR_REPUTATION_TOO_LOW");
        }

        return new CheckReputationResponse(
            UserId: request.UserId.ToString(),
            Allowed: true,
            IsApprovedSeller: true,
            UserStatus: "Active",
            FailureReason: null);
    }

    public async Task<VerifyBidEligibilityResponse> VerifyBidEligibilityAsync(
        VerifyBidEligibilityRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null || request.UserId == Guid.Empty)
        {
            return new VerifyBidEligibilityResponse(
                UserId: request?.UserId.ToString() ?? string.Empty,
                Allowed: false,
                FailureReason: "ERR_USER_NOT_FOUND");
        }

        var user = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.ReputationScore)
            .Include(u => u.PenaltiesReceived)
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user == null || user.IsDeleted || user.Status == UserStatus.Deleted)
        {
            _logger.LogInformation("[VerifyBidEligibility] User '{UserId}' not found or deleted.", request.UserId);
            return new VerifyBidEligibilityResponse(
                UserId: request.UserId.ToString(),
                Allowed: false,
                FailureReason: "ERR_USER_NOT_FOUND");
        }

        if (user.Status == UserStatus.Banned)
        {
            _logger.LogInformation("[VerifyBidEligibility] User '{UserId}' is permanently banned.", request.UserId);
            return new VerifyBidEligibilityResponse(
                UserId: request.UserId.ToString(),
                Allowed: false,
                FailureReason: "ERR_ACCOUNT_BANNED");
        }

        if (user.Status == UserStatus.PendingVerification)
        {
            _logger.LogInformation("[VerifyBidEligibility] User '{UserId}' is unverified.", request.UserId);
            return new VerifyBidEligibilityResponse(
                UserId: request.UserId.ToString(),
                Allowed: false,
                FailureReason: "ERR_ACCOUNT_UNVERIFIED");
        }

        // Check active temporary sanctions / penalties
        var now = DateTimeOffset.UtcNow;
        var hasActiveTemporarySanction = user.PenaltiesReceived.Any(p =>
            p.IsCurrentlyActive() &&
            p.ExpiresAt != null &&
            (p.PenaltyApplied == PenaltyType.Restriction || p.PenaltyApplied == PenaltyType.BanAccount));

        if (hasActiveTemporarySanction)
        {
            _logger.LogInformation("[VerifyBidEligibility] User '{UserId}' has active temporary penalty.", request.UserId);
            return new VerifyBidEligibilityResponse(
                UserId: request.UserId.ToString(),
                Allowed: false,
                FailureReason: "ERR_ACCOUNT_LOCKED_TEMPORARY");
        }

        var currentScore = user.ReputationScore?.CurrentScore ?? DefaultReputationScore;
        if (currentScore < MinimumAllowedReputationScore)
        {
            _logger.LogInformation("[VerifyBidEligibility] User '{UserId}' reputation score ({Score}) is below required minimum ({Min}).",
                request.UserId, currentScore, MinimumAllowedReputationScore);
            return new VerifyBidEligibilityResponse(
                UserId: request.UserId.ToString(),
                Allowed: false,
                FailureReason: "ERR_REPUTATION_TOO_LOW");
        }

        return new VerifyBidEligibilityResponse(
            UserId: request.UserId.ToString(),
            Allowed: true,
            FailureReason: null);
    }

    public async Task<IReadOnlyList<UserPublicInfoDto>> GetBatchPublicInfoAsync(
        BatchPublicInfoRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request?.UserIds == null || request.UserIds.Count == 0)
        {
            return Array.Empty<UserPublicInfoDto>();
        }

        var distinctIds = request.UserIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .Take(100)
            .ToList();

        if (distinctIds.Count == 0)
        {
            return Array.Empty<UserPublicInfoDto>();
        }

        var users = await _dbContext.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(u => distinctIds.Contains(u.Id))
            .Select(u => new
            {
                u.Id,
                u.Username,
                u.FirstName,
                u.LastName,
                IsDeleted = u.IsDeleted || u.Status == UserStatus.Deleted
            })
            .ToListAsync(cancellationToken);

        var result = new List<UserPublicInfoDto>(users.Count);
        foreach (var u in users)
        {
            result.Add(new UserPublicInfoDto(
                UserId: u.Id.ToString(),
                MaskedName: MaskName(u.Username, u.FirstName, u.LastName),
                IsDeleted: u.IsDeleted));
        }

        return result;
    }

    public static string MaskName(string? username, string? firstName, string? lastName)
    {
        string raw;
        if (!string.IsNullOrWhiteSpace(username))
        {
            raw = username.Trim();
        }
        else
        {
            var fullName = $"{lastName} {firstName}".Trim();
            raw = !string.IsNullOrWhiteSpace(fullName) ? fullName : "User";
        }

        if (raw.Length <= 2)
        {
            return $"{raw[0]}*";
        }

        return $"{raw[0]}***{raw[^1]}";
    }
}
