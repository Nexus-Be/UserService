namespace User.Application.Services.Internal;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using User.Application.Interfaces;
using User.Domain.Entities;
using User.Domain.Enums;

public sealed class InternalUserService : IInternalUserService
{
    private readonly IUserDbContext _dbContext;
    private readonly ILogger<InternalUserService> _logger;
    private readonly IMemoryCache? _cache;

    public const int DefaultReputationScore = 50;
    public const int MinimumAllowedReputationScore = 50;

    public InternalUserService(
        IUserDbContext dbContext,
        ILogger<InternalUserService> logger,
        IMemoryCache? cache = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache;
    }

    public async Task<CheckReputationResponse> CheckReputationAsync(
        CheckReputationRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = request?.UserId ?? Guid.Empty;
        var userIdString = userId.ToString();

        // 1. Validate action is "SELL"
        if (string.IsNullOrWhiteSpace(request?.Action) || !string.Equals(request.Action.Trim(), "SELL", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("[CheckReputation] Unsupported action '{Action}' for UserId: '{UserId}'. Expected 'SELL'.", request?.Action, userId);
            return new CheckReputationResponse(
                UserId: userIdString,
                Allowed: false,
                IsApprovedSeller: false,
                UserStatus: "Unknown",
                FailureReason: "ERR_INVALID_ACTION");
        }

        if (userId == Guid.Empty)
        {
            return new CheckReputationResponse(
                UserId: userIdString,
                Allowed: false,
                IsApprovedSeller: false,
                UserStatus: "Unknown",
                FailureReason: "ERR_USER_NOT_FOUND");
        }

        // 2. Fetch user with seller profile, reputation, and penalties
        var user = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.SellerProfile)
            .Include(u => u.ReputationScore)
            .Include(u => u.PenaltiesReceived)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null || user.IsDeleted || user.Status == UserStatus.Deleted)
        {
            _logger.LogInformation("[CheckReputation] User '{UserId}' not found or deleted.", userId);
            return new CheckReputationResponse(
                UserId: userIdString,
                Allowed: false,
                IsApprovedSeller: false,
                UserStatus: "Unknown",
                FailureReason: "ERR_USER_NOT_FOUND");
        }

        var statusName = user.Status.ToString();

        // 3. Check SellerProfile status (Auction requirement: Suspended gets dedicated error code; unapproved gets null)
        if (user.SellerProfile != null && user.SellerProfile.Status == SellerProfileStatus.Suspended)
        {
            _logger.LogInformation("[CheckReputation] User '{UserId}' seller profile is suspended.", userId);
            return new CheckReputationResponse(
                UserId: userIdString,
                Allowed: false,
                IsApprovedSeller: false,
                UserStatus: statusName,
                FailureReason: "ERR_SELLER_SUSPENDED");
        }

        if (user.SellerProfile == null || user.SellerProfile.Status != SellerProfileStatus.Approved)
        {
            _logger.LogInformation("[CheckReputation] User '{UserId}' is not an approved seller. Status: '{Status}'",
                userId, user.SellerProfile?.Status);
            return new CheckReputationResponse(
                UserId: userIdString,
                Allowed: false,
                IsApprovedSeller: false,
                UserStatus: statusName,
                FailureReason: null);
        }

        // 4. User is an approved seller; now evaluate qualification to open auction session (Banned, Locked, Reputation)
        var baseResult = await EvaluateUserEligibilityAsync(
            user,
            TargetRole.Seller,
            cancellationToken);

        if (!baseResult.Allowed)
        {
            return new CheckReputationResponse(
                UserId: userIdString,
                Allowed: false,
                IsApprovedSeller: false,
                UserStatus: baseResult.UserStatus,
                FailureReason: baseResult.FailureReason);
        }

        return new CheckReputationResponse(
            UserId: userIdString,
            Allowed: true,
            IsApprovedSeller: true,
            UserStatus: baseResult.UserStatus,
            FailureReason: null);
    }

    public async Task<VerifyBidEligibilityResponse> VerifyBidEligibilityAsync(
        VerifyBidEligibilityRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = request?.UserId ?? Guid.Empty;

        var baseResult = await EvaluateBaseEligibilityAsync(
            userId,
            TargetRole.Buyer,
            cancellationToken);

        if (!baseResult.Allowed)
        {
            return new VerifyBidEligibilityResponse(
                UserId: userId.ToString(),
                Allowed: false,
                FailureReason: baseResult.FailureReason);
        }

        return new VerifyBidEligibilityResponse(
            UserId: userId.ToString(),
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

    private sealed record UserBaseEligibilityResult(
        bool Allowed,
        Domain.Entities.User? User,
        string UserStatus,
        string? FailureReason
    );

    private async Task<UserBaseEligibilityResult> EvaluateBaseEligibilityAsync(
        Guid userId,
        TargetRole targetRole,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            return new UserBaseEligibilityResult(false, null, "Unknown", "ERR_USER_NOT_FOUND");
        }

        var user = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.SellerProfile)
            .Include(u => u.ReputationScore)
            .Include(u => u.PenaltiesReceived)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null || user.IsDeleted || user.Status == UserStatus.Deleted)
        {
            _logger.LogInformation("[InternalUser] User '{UserId}' not found or deleted.", userId);
            return new UserBaseEligibilityResult(false, null, "Unknown", "ERR_USER_NOT_FOUND");
        }

        return await EvaluateUserEligibilityAsync(user, targetRole, cancellationToken);
    }

    private async Task<UserBaseEligibilityResult> EvaluateUserEligibilityAsync(
        Domain.Entities.User user,
        TargetRole targetRole,
        CancellationToken cancellationToken)
    {
        var statusName = user.Status.ToString();

        if (user.Status == UserStatus.Banned)
        {
            _logger.LogInformation("[InternalUser] User '{UserId}' is permanently banned.", user.Id);
            return new UserBaseEligibilityResult(false, user, statusName, "ERR_ACCOUNT_BANNED");
        }

        if (user.Status == UserStatus.PendingVerification)
        {
            _logger.LogInformation("[InternalUser] User '{UserId}' is unverified.", user.Id);
            return new UserBaseEligibilityResult(false, user, statusName, "ERR_ACCOUNT_UNVERIFIED");
        }

        // Check active temporary sanctions / penalties targeted at the given role or the entire account
        var hasActiveTemporarySanction = user.PenaltiesReceived.Any(p =>
            p.IsCurrentlyActive() &&
            (p.TargetRole == targetRole || p.TargetRole == TargetRole.Account) &&
            (p.PenaltyApplied == PenaltyType.Restriction || p.PenaltyApplied == PenaltyType.BanAccount));

        if (hasActiveTemporarySanction)
        {
            _logger.LogInformation("[InternalUser] User '{UserId}' has active temporary penalty for role '{Role}'.", user.Id, targetRole);
            return new UserBaseEligibilityResult(false, user, statusName, "ERR_ACCOUNT_LOCKED_TEMPORARY");
        }

        // Check Reputation Score against dynamic SystemConfig threshold
        var minRequiredScore = await GetMinimumReputationScoreAsync(targetRole, cancellationToken);
        var currentScore = user.ReputationScore?.CurrentScore ?? DefaultReputationScore;
        if (currentScore < minRequiredScore)
        {
            _logger.LogInformation("[InternalUser] User '{UserId}' reputation score ({Score}) is below required minimum ({Min}) for role '{Role}'.",
                user.Id, currentScore, minRequiredScore, targetRole);
            return new UserBaseEligibilityResult(false, user, statusName, "ERR_REPUTATION_TOO_LOW");
        }

        return new UserBaseEligibilityResult(true, user, statusName, null);
    }

    private async Task<int> GetMinimumReputationScoreAsync(TargetRole targetRole, CancellationToken cancellationToken)
    {
        var configKey = targetRole == TargetRole.Seller
            ? "AUCTION_MIN_REPUTATION_CREATE"
            : "AUCTION_MIN_REPUTATION_BID";

        var cacheKey = $"user:config:{configKey}";
        if (_cache != null && _cache.TryGetValue(cacheKey, out int cachedScore))
        {
            return cachedScore;
        }

        var config = await _dbContext.SystemConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ConfigKey == configKey && !c.IsDeleted, cancellationToken);

        var minScore = DefaultReputationScore;
        if (config != null && int.TryParse(config.ConfigValue, out var parsed))
        {
            minScore = parsed;
        }

        _cache?.Set(cacheKey, minScore, TimeSpan.FromMinutes(1));
        return minScore;
    }
}
