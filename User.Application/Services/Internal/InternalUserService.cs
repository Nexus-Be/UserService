namespace User.Application.Services.Internal;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using User.Application.Interfaces;
using User.Domain.Entities;
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
                FailureReason: "ERR_ACCOUNT_UNVERIFIED");
        }

        // 2. Base eligibility with TargetRole.Seller
        var baseResult = await EvaluateBaseEligibilityAsync(
            userId,
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

        // 3. Check SellerProfile status
        var user = baseResult.User!;
        var isApprovedSeller = user.SellerProfile != null && user.SellerProfile.Status == SellerProfileStatus.Approved;
        if (!isApprovedSeller)
        {
            _logger.LogInformation("[CheckReputation] User '{UserId}' is not an approved seller.", userId);
            return new CheckReputationResponse(
                UserId: userIdString,
                Allowed: false,
                IsApprovedSeller: false,
                UserStatus: baseResult.UserStatus,
                FailureReason: "ERR_ACCOUNT_UNVERIFIED");
        }

        return new CheckReputationResponse(
            UserId: userIdString,
            Allowed: true,
            IsApprovedSeller: true,
            UserStatus: "Active",
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

        var statusName = user.Status.ToString();

        if (user.Status == UserStatus.Banned)
        {
            _logger.LogInformation("[InternalUser] User '{UserId}' is permanently banned.", userId);
            return new UserBaseEligibilityResult(false, user, statusName, "ERR_ACCOUNT_BANNED");
        }

        if (user.Status == UserStatus.PendingVerification)
        {
            _logger.LogInformation("[InternalUser] User '{UserId}' is unverified.", userId);
            return new UserBaseEligibilityResult(false, user, statusName, "ERR_ACCOUNT_UNVERIFIED");
        }

        // Check active temporary sanctions / penalties targeted at the given role or the entire account
        var hasActiveTemporarySanction = user.PenaltiesReceived.Any(p =>
            p.IsCurrentlyActive() &&
            (p.TargetRole == targetRole || p.TargetRole == TargetRole.Account) &&
            (p.PenaltyApplied == PenaltyType.Restriction || p.PenaltyApplied == PenaltyType.BanAccount));

        if (hasActiveTemporarySanction)
        {
            _logger.LogInformation("[InternalUser] User '{UserId}' has active temporary penalty for role '{Role}'.", userId, targetRole);
            return new UserBaseEligibilityResult(false, user, statusName, "ERR_ACCOUNT_LOCKED_TEMPORARY");
        }

        // Check Reputation Score
        var currentScore = user.ReputationScore?.CurrentScore ?? DefaultReputationScore;
        if (currentScore < MinimumAllowedReputationScore)
        {
            _logger.LogInformation("[InternalUser] User '{UserId}' reputation score ({Score}) is below required minimum ({Min}).",
                userId, currentScore, MinimumAllowedReputationScore);
            return new UserBaseEligibilityResult(false, user, statusName, "ERR_REPUTATION_TOO_LOW");
        }

        return new UserBaseEligibilityResult(true, user, statusName, null);
    }
}
