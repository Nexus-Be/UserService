namespace User.Application.Services.Internal;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using User.Application.Common.Extensions;
using User.Application.Interfaces;
using User.Domain.Constants;
using User.Domain.Entities;
using User.Domain.Enums;
using User.Domain.Exceptions;

public sealed class InternalUserService : IInternalUserService
{
    private const string BidMinScoreConfigKey = "AUCTION_MIN_REPUTATION_BID";
    private const string CreateAuctionMinScoreConfigKey = "AUCTION_MIN_REPUTATION_CREATE";

    private static readonly TimeSpan ConfigCacheDuration = TimeSpan.FromMinutes(5);

    private readonly IUserDbContext _dbContext;
    private readonly IMemoryCache _cache;
    private readonly ILogger<InternalUserService> _logger;

    public InternalUserService(
        IUserDbContext dbContext,
        IMemoryCache cache,
        ILogger<InternalUserService> logger)
    {
        _dbContext = dbContext;
        _cache = cache;
        _logger = logger;
    }

    public async Task<CheckReputationResponse> CheckReputationAsync(
        CheckReputationRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[InternalUserService] Checking seller reputation for UserId: '{UserId}', Action: '{Action}'",
            request.UserId,
            request.EffectiveAction);

        var user = await _dbContext.Users
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Include(u => u.ReputationScore)
            .Include(u => u.SellerProfile)
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user is null)
        {
            return new CheckReputationResponse
            {
                UserId = request.UserId,
                Allowed = false,
                IsApprovedSeller = false,
                UserStatus = "NotFound",
                CurrentScore = 0,
                TrustLevel = TrustLevel.Untrusted.ToEnumString(),
                FailureReason = "USER_NOT_FOUND"
            };
        }

        var isDeleted = user.IsDeleted || user.Status == UserStatus.Deleted;
        var currentScore = user.ReputationScore?.SellerScore ?? ReputationRules.DefaultInitialScore;
        var trustLevel = (user.ReputationScore?.SellerTrustLevel ?? TrustLevel.Standard).ToEnumString();
        var isApprovedSeller = user.SellerProfile != null && !user.SellerProfile.IsDeleted && user.SellerProfile.Status == SellerProfileStatus.Approved;

        if (isDeleted)
        {
            return new CheckReputationResponse
            {
                UserId = request.UserId,
                Allowed = false,
                IsApprovedSeller = false,
                UserStatus = UserStatus.Deleted.ToEnumString(),
                CurrentScore = currentScore,
                TrustLevel = trustLevel,
                FailureReason = "USER_DELETED"
            };
        }

        if (user.Status == UserStatus.Banned)
        {
            return new CheckReputationResponse
            {
                UserId = request.UserId,
                Allowed = false,
                IsApprovedSeller = false,
                UserStatus = UserStatus.Banned.ToEnumString(),
                CurrentScore = currentScore,
                TrustLevel = trustLevel,
                FailureReason = "ACCOUNT_BANNED"
            };
        }

        if (user.Status != UserStatus.Active)
        {
            return new CheckReputationResponse
            {
                UserId = request.UserId,
                Allowed = false,
                IsApprovedSeller = false,
                UserStatus = user.Status.ToEnumString(),
                CurrentScore = currentScore,
                TrustLevel = trustLevel,
                FailureReason = "ACCOUNT_INACTIVE"
            };
        }

        if (!isApprovedSeller)
        {
            return new CheckReputationResponse
            {
                UserId = request.UserId,
                Allowed = false,
                IsApprovedSeller = false,
                UserStatus = user.Status.ToEnumString(),
                CurrentScore = currentScore,
                TrustLevel = trustLevel,
                FailureReason = "SELLER_NOT_APPROVED"
            };
        }

        var now = DateTimeOffset.UtcNow;
        var activePenalties = await _dbContext.UserPenalties
            .AsNoTracking()
            .Where(p => p.UserId == request.UserId
                        && p.Status == PenaltyStatus.Active
                        && (p.TargetRole == TargetRole.Account || p.TargetRole == TargetRole.Seller)
                        && (p.ExpiresAt == null || p.ExpiresAt > now))
            .ToListAsync(cancellationToken);

        if (activePenalties.Any(p => p.PenaltyApplied == PenaltyType.BanAccount))
        {
            return new CheckReputationResponse
            {
                UserId = request.UserId,
                Allowed = false,
                IsApprovedSeller = true,
                UserStatus = user.Status.ToEnumString(),
                CurrentScore = currentScore,
                TrustLevel = trustLevel,
                FailureReason = "ACCOUNT_BANNED"
            };
        }

        if (activePenalties.Any(p => p.PenaltyApplied == PenaltyType.Restriction))
        {
            return new CheckReputationResponse
            {
                UserId = request.UserId,
                Allowed = false,
                IsApprovedSeller = true,
                UserStatus = user.Status.ToEnumString(),
                CurrentScore = currentScore,
                TrustLevel = trustLevel,
                FailureReason = "ACCOUNT_RESTRICTED"
            };
        }

        var minRequiredScore = await GetThresholdScoreAsync(CreateAuctionMinScoreConfigKey, ReputationRules.DefaultMinScoreToSell, cancellationToken);
        if (currentScore < minRequiredScore)
        {
            return new CheckReputationResponse
            {
                UserId = request.UserId,
                Allowed = false,
                IsApprovedSeller = true,
                UserStatus = user.Status.ToEnumString(),
                CurrentScore = currentScore,
                TrustLevel = trustLevel,
                FailureReason = "REPUTATION_TOO_LOW"
            };
        }

        return new CheckReputationResponse
        {
            UserId = request.UserId,
            Allowed = true,
            IsApprovedSeller = true,
            UserStatus = "Active",
            CurrentScore = currentScore,
            TrustLevel = trustLevel,
            FailureReason = null
        };
    }

    public async Task<VerifyBidEligibilityResponse> VerifyBidEligibilityAsync(
        VerifyBidEligibilityRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[InternalUserService] Verifying buyer bid eligibility for UserId: '{UserId}', AuctionId: '{AuctionId}'",
            request.UserId,
            request.AuctionId);

        var user = await _dbContext.Users
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Include(u => u.ReputationScore)
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user is null)
        {
            return new VerifyBidEligibilityResponse
            {
                UserId = request.UserId,
                Allowed = false,
                CurrentScore = 0,
                FailureReason = "USER_NOT_FOUND"
            };
        }

        var isDeleted = user.IsDeleted || user.Status == UserStatus.Deleted;
        var currentScore = user.ReputationScore?.CurrentScore ?? ReputationRules.DefaultInitialScore;

        if (isDeleted)
        {
            return new VerifyBidEligibilityResponse
            {
                UserId = request.UserId,
                Allowed = false,
                CurrentScore = currentScore,
                FailureReason = "USER_DELETED"
            };
        }

        if (user.Status == UserStatus.Banned)
        {
            return new VerifyBidEligibilityResponse
            {
                UserId = request.UserId,
                Allowed = false,
                CurrentScore = currentScore,
                FailureReason = "ACCOUNT_BANNED"
            };
        }

        if (user.Status != UserStatus.Active)
        {
            return new VerifyBidEligibilityResponse
            {
                UserId = request.UserId,
                Allowed = false,
                CurrentScore = currentScore,
                FailureReason = "ACCOUNT_INACTIVE"
            };
        }

        var now = DateTimeOffset.UtcNow;
        var activePenalties = await _dbContext.UserPenalties
            .AsNoTracking()
            .Where(p => p.UserId == request.UserId
                        && p.Status == PenaltyStatus.Active
                        && (p.TargetRole == TargetRole.Account || p.TargetRole == TargetRole.Buyer)
                        && (p.ExpiresAt == null || p.ExpiresAt > now))
            .ToListAsync(cancellationToken);

        if (activePenalties.Any(p => p.PenaltyApplied == PenaltyType.BanAccount))
        {
            return new VerifyBidEligibilityResponse
            {
                UserId = request.UserId,
                Allowed = false,
                CurrentScore = currentScore,
                FailureReason = "ACCOUNT_BANNED"
            };
        }

        if (activePenalties.Any(p => p.PenaltyApplied == PenaltyType.Restriction))
        {
            return new VerifyBidEligibilityResponse
            {
                UserId = request.UserId,
                Allowed = false,
                CurrentScore = currentScore,
                FailureReason = "ACCOUNT_RESTRICTED"
            };
        }

        var minRequiredScore = await GetThresholdScoreAsync(BidMinScoreConfigKey, ReputationRules.DefaultMinScoreToBid, cancellationToken);
        if (currentScore < minRequiredScore)
        {
            return new VerifyBidEligibilityResponse
            {
                UserId = request.UserId,
                Allowed = false,
                CurrentScore = currentScore,
                FailureReason = "REPUTATION_TOO_LOW"
            };
        }

        return new VerifyBidEligibilityResponse
        {
            UserId = request.UserId,
            Allowed = true,
            CurrentScore = currentScore,
            FailureReason = null
        };
    }

    public async Task<BatchPublicInfoResponse> GetBatchPublicInfoAsync(
        BatchPublicInfoRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.UserIds is null || request.UserIds.Count == 0)
        {
            return new BatchPublicInfoResponse { Users = Array.Empty<UserPublicInfoItem>() };
        }

        if (request.UserIds.Count > 100)
        {
            throw new DomainException("VAL_INVALID_INPUT", "User IDs list cannot exceed 100 identifiers per batch request.", 400);
        }

        var distinctIds = request.UserIds.Distinct().ToList();

        var users = await _dbContext.Users
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Include(u => u.ReputationScore)
            .Include(u => u.SellerProfile)
            .Where(u => distinctIds.Contains(u.Id))
            .Select(u => new
            {
                u.Id,
                u.FirstName,
                u.LastName,
                u.Username,
                u.AvatarUrl,
                u.IsDeleted,
                u.Status,
                ShopName = u.SellerProfile != null ? u.SellerProfile.ShopName : null,
                BuyerScore = u.ReputationScore != null ? u.ReputationScore.CurrentScore : ReputationRules.DefaultInitialScore,
                SellerScore = u.ReputationScore != null ? u.ReputationScore.SellerScore : ReputationRules.DefaultInitialScore
            })
            .ToListAsync(cancellationToken);

        var items = users.Select(u =>
        {
            var fullName = $"{u.FirstName} {u.LastName}".Trim();
            var displayName = !string.IsNullOrWhiteSpace(u.ShopName)
                ? u.ShopName
                : !string.IsNullOrWhiteSpace(fullName)
                    ? fullName
                    : u.Username ?? "Anonymous";

            var rawForMask = !string.IsNullOrWhiteSpace(fullName)
                ? fullName
                : u.Username ?? displayName;
            var maskedName = rawForMask.Length > 2
                ? $"{rawForMask[0]}***{rawForMask[^1]}"
                : "u***r";

            var isDeleted = u.IsDeleted || u.Status == UserStatus.Deleted;

            return new UserPublicInfoItem
            {
                UserId = u.Id,
                DisplayName = displayName,
                MaskedDisplayName = maskedName,
                MaskedName = maskedName,
                IsDeleted = isDeleted,
                AvatarUrl = u.AvatarUrl,
                ReputationScore = u.BuyerScore,
                SellerReputationScore = u.SellerScore
            };
        }).ToList();

        return new BatchPublicInfoResponse { Users = items };
    }

    public async Task<InternalPenaltyResponse> ApplyInternalPenaltyAsync(
        InternalPenaltyRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ReferenceId))
        {
            throw new DomainException("VAL_INVALID_INPUT", "Reference ID is required for internal penalty processing.", 400);
        }

        var user = await _dbContext.Users
            .Include(u => u.ReputationScore)
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user == null)
        {
            throw new DomainException("USER_NOT_FOUND", "User account was not found for the specified identifier.", 404);
        }

        var normalizedRefId = request.ReferenceId.Trim();
        var effectiveTrack = request.TargetRole == TargetRole.Seller ? TargetRole.Seller : TargetRole.Buyer;
        var logReason = request.ViolationType switch
        {
            PenaltyViolationType.PaymentTimeout or PenaltyViolationType.AuctionNonPayment => ReputationChangeReason.AuctionPaymentTimeout,
            PenaltyViolationType.CancellationWithoutReason => ReputationChangeReason.SellerCancellation,
            _ => ReputationChangeReason.ViolationPenalty
        };

        var repScore = EnsureReputationScore(user);

        // Idempotency check by (UserId, TargetRole, ReferenceType, ReferenceId, Reason)
        var existingLog = await _dbContext.ReputationLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(l =>
                l.UserId == request.UserId &&
                l.TargetRole == effectiveTrack &&
                l.ReferenceType == request.ReferenceType &&
                l.ReferenceId == normalizedRefId &&
                l.Reason == logReason,
                cancellationToken);

        if (existingLog != null)
        {
            var currentScore = effectiveTrack == TargetRole.Seller ? repScore.SellerScore : repScore.CurrentScore;
            var currentTrust = effectiveTrack == TargetRole.Seller ? repScore.SellerTrustLevel.ToEnumString() : repScore.TrustLevel.ToEnumString();
            return new InternalPenaltyResponse
            {
                UserId = request.UserId,
                TargetRole = effectiveTrack.ToEnumString(),
                IsDuplicate = true,
                PenaltyId = null,
                PreviousScore = currentScore,
                NewScore = currentScore,
                AppliedDelta = 0,
                NewTrustLevel = currentTrust
            };
        }

        int rawPenaltyPoints;
        if (request.CustomScoreDeduction.HasValue && request.CustomScoreDeduction.Value > 0)
        {
            rawPenaltyPoints = request.CustomScoreDeduction.Value;
        }
        else if (effectiveTrack == TargetRole.Seller)
        {
            rawPenaltyPoints = await GetThresholdScoreAsync("SELLER_CANCEL_PENALTY_SCORE", Math.Abs(ReputationRules.SellerCancelPenalty), cancellationToken);
        }
        else
        {
            rawPenaltyPoints = await GetThresholdScoreAsync("PAYMENT_TIMEOUT_PENALTY_SCORE", Math.Abs(ReputationRules.PaymentTimeoutPenalty), cancellationToken);
        }

        var (oldScore, newScore, appliedDelta) = repScore.ApplyScoreChange(
            effectiveTrack,
            -Math.Abs(rawPenaltyPoints),
            isPenalty: true);

        var newTrustLevel = effectiveTrack == TargetRole.Seller
            ? repScore.SellerTrustLevel.ToEnumString()
            : repScore.TrustLevel.ToEnumString();

        var now = DateTimeOffset.UtcNow;

        var penalty = new UserPenalty
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            TargetRole = effectiveTrack,
            ViolationType = request.ViolationType,
            Reason = request.Reason.Trim(),
            PenaltyApplied = PenaltyType.DeductPoints,
            PointsDeducted = Math.Abs(appliedDelta),
            Status = PenaltyStatus.Active,
            PolicyClauseCode = request.PolicyClauseCode,
            IssuedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
        _dbContext.UserPenalties.Add(penalty);

        _dbContext.ReputationLogs.Add(new ReputationLog
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            TargetRole = effectiveTrack,
            ChangeAmount = appliedDelta,
            ScoreAfter = newScore,
            Reason = logReason,
            ReferenceType = request.ReferenceType,
            ReferenceId = normalizedRefId,
            Note = request.Reason.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new InternalPenaltyResponse
        {
            UserId = request.UserId,
            TargetRole = effectiveTrack.ToEnumString(),
            IsDuplicate = false,
            PenaltyId = penalty.Id,
            PreviousScore = oldScore,
            NewScore = newScore,
            AppliedDelta = appliedDelta,
            NewTrustLevel = newTrustLevel
        };
    }

    public async Task<InternalReputationAdjustResponse> ApplyInternalReputationAdjustAsync(
        InternalReputationAdjustRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ReferenceId))
        {
            throw new DomainException("VAL_INVALID_INPUT", "Reference ID is required for internal reputation adjustment.", 400);
        }

        var user = await _dbContext.Users
            .Include(u => u.ReputationScore)
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user == null)
        {
            throw new DomainException("USER_NOT_FOUND", "User account was not found for the specified identifier.", 404);
        }

        var normalizedRefId = request.ReferenceId.Trim();
        var effectiveTrack = request.TargetRole == TargetRole.Seller ? TargetRole.Seller : TargetRole.Buyer;
        var repScore = EnsureReputationScore(user);

        // 1. Idempotency check by (UserId, TargetRole, ReferenceType, ReferenceId, Reason)
        var existingLog = await _dbContext.ReputationLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(l =>
                l.UserId == request.UserId &&
                l.TargetRole == effectiveTrack &&
                l.ReferenceType == request.ReferenceType &&
                l.ReferenceId == normalizedRefId &&
                l.Reason == request.Reason,
                cancellationToken);

        if (existingLog != null)
        {
            var currentScore = effectiveTrack == TargetRole.Seller ? repScore.SellerScore : repScore.CurrentScore;
            var currentTrust = effectiveTrack == TargetRole.Seller ? repScore.SellerTrustLevel.ToEnumString() : repScore.TrustLevel.ToEnumString();
            return new InternalReputationAdjustResponse
            {
                UserId = request.UserId,
                TargetRole = effectiveTrack.ToEnumString(),
                IsDuplicate = true,
                CooldownSuppressed = false,
                PreviousScore = currentScore,
                NewScore = currentScore,
                AppliedDelta = 0,
                NewTrustLevel = currentTrust
            };
        }

        var now = DateTimeOffset.UtcNow;
        var effectiveDelta = request.ScoreDelta;
        var cooldownSuppressed = false;

        // 2. 7-Day Anti-Farming Cooldown check for positive rewards between the same trading pair
        if (effectiveDelta > 0 && request.PartnerUserId.HasValue)
        {
            var cooldownDays = await GetThresholdScoreAsync("ANTI_FARMING_COOLDOWN_DAYS", ReputationRules.AntiFarmingCooldownDays, cancellationToken);
            var cutoff = now.AddDays(-cooldownDays);

            var recentlyRewarded = await _dbContext.ReputationLogs
                .AsNoTracking()
                .AnyAsync(l =>
                    l.UserId == request.UserId &&
                    l.TargetRole == effectiveTrack &&
                    l.PartnerUserId == request.PartnerUserId.Value &&
                    l.ChangeAmount > 0 &&
                    l.CreatedAt >= cutoff,
                    cancellationToken);

            if (recentlyRewarded)
            {
                effectiveDelta = 0;
                cooldownSuppressed = true;
            }
        }

        var (oldScore, newScore, appliedDelta) = repScore.ApplyScoreChange(effectiveTrack, effectiveDelta);
        var newTrustLevel = effectiveTrack == TargetRole.Seller
            ? repScore.SellerTrustLevel.ToEnumString()
            : repScore.TrustLevel.ToEnumString();

        if (appliedDelta != 0)
        {
            _dbContext.ReputationLogs.Add(new ReputationLog
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                TargetRole = effectiveTrack,
                ChangeAmount = appliedDelta,
                ScoreAfter = newScore,
                Reason = request.Reason,
                ReferenceType = request.ReferenceType,
                ReferenceId = normalizedRefId,
                PartnerUserId = request.PartnerUserId,
                Note = request.Note,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new InternalReputationAdjustResponse
        {
            UserId = request.UserId,
            TargetRole = effectiveTrack.ToEnumString(),
            IsDuplicate = false,
            CooldownSuppressed = cooldownSuppressed,
            PreviousScore = oldScore,
            NewScore = newScore,
            AppliedDelta = appliedDelta,
            NewTrustLevel = newTrustLevel
        };
    }

    private ReputationScore EnsureReputationScore(User user)
    {
        if (user.ReputationScore != null)
        {
            return user.ReputationScore;
        }

        var repScore = ReputationScore.CreateDefault(user.Id);
        _dbContext.ReputationScores.Add(repScore);
        return repScore;
    }

    private async Task<(bool IsEligible, int CurrentScore, string TrustLevel, string? FailureReason)> EvaluateUserEligibilityAsync(
        Guid userId,
        TargetRole targetRole,
        string configKey,
        int defaultMinScore,
        CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.ReputationScore)
            .Include(u => u.SellerProfile)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            return (false, 0, TrustLevel.Untrusted.ToEnumString(), "ERR_USER_NOT_FOUND");
        }

        // Select the isolated track score: SellerScore for Seller actions, CurrentScore for Buyer actions
        var currentScore = targetRole == TargetRole.Seller
            ? (user.ReputationScore?.SellerScore ?? ReputationRules.DefaultInitialScore)
            : (user.ReputationScore?.CurrentScore ?? ReputationRules.DefaultInitialScore);

        var trustLevel = targetRole == TargetRole.Seller
            ? (user.ReputationScore?.SellerTrustLevel ?? TrustLevel.Standard).ToEnumString()
            : (user.ReputationScore?.TrustLevel ?? TrustLevel.Standard).ToEnumString();

        if (user.Status == UserStatus.Banned || user.Status == UserStatus.Deleted)
        {
            return (false, currentScore, trustLevel, "ERR_ACCOUNT_BANNED");
        }

        if (user.Status != UserStatus.Active)
        {
            return (false, currentScore, trustLevel, "ERR_ACCOUNT_RESTRICTED");
        }

        if (targetRole == TargetRole.Seller &&
            user.SellerProfile is not null &&
            user.SellerProfile.Status != SellerProfileStatus.Approved)
        {
            return (false, currentScore, trustLevel, "ERR_ACCOUNT_RESTRICTED");
        }

        var now = DateTimeOffset.UtcNow;

        var activePenalties = await _dbContext.UserPenalties
            .AsNoTracking()
            .Where(p => p.UserId == userId
                        && p.Status == PenaltyStatus.Active
                        && (p.TargetRole == TargetRole.Account || p.TargetRole == targetRole)
                        && (p.ExpiresAt == null || p.ExpiresAt > now))
            .ToListAsync(cancellationToken);

        if (activePenalties.Any(p => p.PenaltyApplied == PenaltyType.BanAccount))
        {
            return (false, currentScore, trustLevel, "ERR_ACCOUNT_BANNED");
        }

        if (activePenalties.Any(p => p.PenaltyApplied == PenaltyType.Restriction))
        {
            return (false, currentScore, trustLevel, "ERR_ACCOUNT_RESTRICTED");
        }

        var minRequiredScore = await GetThresholdScoreAsync(configKey, defaultMinScore, cancellationToken);

        if (currentScore < minRequiredScore)
        {
            return (false, currentScore, trustLevel, "ERR_REPUTATION_TOO_LOW");
        }

        return (true, currentScore, trustLevel, null);
    }

    private async Task<int> GetThresholdScoreAsync(string configKey, int defaultValue, CancellationToken cancellationToken)
    {
        var cacheKey = $"sys_config:{configKey}";
        if (_cache.TryGetValue(cacheKey, out int cachedThreshold))
        {
            return cachedThreshold;
        }

        var config = await _dbContext.SystemConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ConfigKey == configKey, cancellationToken);

        var resolvedValue = defaultValue;
        if (config is not null && int.TryParse(config.ConfigValue, out var parsed))
        {
            resolvedValue = parsed;
        }

        _cache.Set(cacheKey, resolvedValue, ConfigCacheDuration);
        return resolvedValue;
    }
}
