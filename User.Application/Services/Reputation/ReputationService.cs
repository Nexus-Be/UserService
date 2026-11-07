namespace User.Application.Services.Reputation;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using User.Application.Common;
using User.Application.Common.Extensions;
using User.Application.Common.Models;
using User.Application.Interfaces;
using User.Domain.Constants;
using User.Domain.Entities;
using User.Domain.Enums;
using User.Domain.Exceptions;

public sealed class ReputationService : IReputationService
{
    private readonly IUserDbContext _dbContext;
    private readonly ILogger<ReputationService> _logger;

    public ReputationService(IUserDbContext dbContext, ILogger<ReputationService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<MyReputationResponse> GetMyReputationAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.ReputationScore)
            .Include(u => u.SellerProfile)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null)
        {
            throw new DomainException("USER_NOT_FOUND", "User account was not found for the specified identifier.", 404);
        }

        var score = user.ReputationScore ?? ReputationScore.CreateDefault(userId, Guid.Empty);

        var now = DateTimeOffset.UtcNow;
        var activePenalties = await _dbContext.UserPenalties
            .AsNoTracking()
            .Where(p => p.UserId == userId && p.Status == PenaltyStatus.Active && (p.ExpiresAt == null || p.ExpiresAt > now))
            .ToListAsync(cancellationToken);

        var minBidScore = await GetConfigIntAsync("AUCTION_MIN_REPUTATION_BID", ReputationRules.DefaultMinScoreToBid, cancellationToken);
        var minSellScore = await GetConfigIntAsync("AUCTION_MIN_REPUTATION_CREATE", ReputationRules.DefaultMinScoreToSell, cancellationToken);

        var hasBuyerBlock = activePenalties.Any(p =>
            (p.TargetRole == TargetRole.Buyer || p.TargetRole == TargetRole.Account) &&
            (p.PenaltyApplied == PenaltyType.Restriction || p.PenaltyApplied == PenaltyType.BanAccount));

        var hasSellerBlock = activePenalties.Any(p =>
            (p.TargetRole == TargetRole.Seller || p.TargetRole == TargetRole.Account) &&
            (p.PenaltyApplied == PenaltyType.Restriction || p.PenaltyApplied == PenaltyType.BanAccount));

        var canBid = user.Status == UserStatus.Active && !hasBuyerBlock && score.CurrentScore >= minBidScore;
        var canSell = user.Status == UserStatus.Active
            && user.SellerProfile != null
            && user.SellerProfile.Status == SellerProfileStatus.Approved
            && !hasSellerBlock
            && score.SellerScore >= minSellScore;

        return new MyReputationResponse
        {
            UserId = userId,
            CurrentScore = score.CurrentScore,
            TrustLevel = score.TrustLevel.ToEnumString(),
            CanBid = canBid,
            TotalPositiveReviews = score.TotalPositiveReviews,
            TotalNegativeReviews = score.TotalNegativeReviews,
            PenaltyCount = score.PenaltyCount,
            NextLevelThreshold = ReputationRules.GetNextLevelThreshold(score.CurrentScore),
            SellerScore = score.SellerScore,
            SellerTrustLevel = score.SellerTrustLevel.ToEnumString(),
            CanSell = canSell,
            SellerPositiveReviews = score.SellerPositiveReviews,
            SellerNegativeReviews = score.SellerNegativeReviews,
            SellerPenaltyCount = score.SellerPenaltyCount,
            SellerNextLevelThreshold = ReputationRules.GetNextLevelThreshold(score.SellerScore),
            UpdatedAt = score.UpdatedAt
        };
    }

    public async Task<PublicReputationResponse> GetUserReputationSummaryAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.ReputationScore)
            .Include(u => u.SellerProfile)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null)
        {
            throw new DomainException("USER_NOT_FOUND", "User account was not found for the specified identifier.", 404);
        }

        var score = user.ReputationScore ?? ReputationScore.CreateDefault(userId, Guid.Empty);

        var totalPos = score.TotalPositiveReviews + score.SellerPositiveReviews;
        var totalNeg = score.TotalNegativeReviews + score.SellerNegativeReviews;
        var totalReviews = totalPos + totalNeg;
        var positiveRatio = totalReviews > 0
            ? Math.Round((decimal)totalPos * 100m / totalReviews, 2)
            : 100m;

        var badges = new List<string>();
        if (score.CurrentScore >= 70) badges.Add("TrustedBuyer");
        if (score.CurrentScore >= 90) badges.Add("EliteBuyer");
        if (user.SellerProfile?.Status == SellerProfileStatus.Approved)
        {
            badges.Add("VerifiedSeller");
            if (score.SellerScore >= 70) badges.Add("TrustedSeller");
            if (score.SellerScore >= 90) badges.Add("TopRatedSeller");
        }

        return new PublicReputationResponse
        {
            UserId = userId,
            CurrentScore = score.CurrentScore,
            TrustLevel = score.TrustLevel.ToEnumString(),
            TotalPositiveReviews = score.TotalPositiveReviews,
            TotalNegativeReviews = score.TotalNegativeReviews,
            TotalReviews = totalReviews,
            PositiveRatio = positiveRatio,
            SellerScore = score.SellerScore,
            SellerTrustLevel = score.SellerTrustLevel.ToEnumString(),
            SellerPositiveReviews = score.SellerPositiveReviews,
            SellerNegativeReviews = score.SellerNegativeReviews,
            Badges = badges,
            MemberSince = user.CreatedAt
        };
    }

    public async Task<PagedData<ReputationHistoryItemDto>> GetUserReputationHistoryAsync(
        Guid targetUserId,
        Guid requesterId,
        bool isStaffOrAdmin,
        int page = 1,
        int pageSize = 20,
        TargetRole? targetRole = null,
        CancellationToken cancellationToken = default)
    {
        if (requesterId != targetUserId && !isStaffOrAdmin)
        {
            throw new DomainException("FORBIDDEN", "You do not have permission to view another user's reputation history.", 403);
        }

        var userExists = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == targetUserId, cancellationToken);

        if (!userExists)
        {
            throw new DomainException("USER_NOT_FOUND", "User account was not found for the specified identifier.", 404);
        }

        var (safePage, safeLimit) = PaginationConstants.Normalize(page, pageSize);

        var query = _dbContext.ReputationLogs
            .AsNoTracking()
            .Where(l => l.UserId == targetUserId);

        if (targetRole.HasValue)
        {
            query = query.Where(l => l.TargetRole == targetRole.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((safePage - 1) * safeLimit)
            .Take(safeLimit)
            .Select(l => new ReputationHistoryItemDto
            {
                LogId = l.Id,
                TargetRole = l.TargetRole.ToEnumString(),
                ChangeAmount = l.ChangeAmount,
                ScoreAfter = l.ScoreAfter,
                Reason = l.Reason.ToEnumString(),
                ReferenceType = l.ReferenceType.HasValue ? l.ReferenceType.Value.ToEnumString() : null,
                ReferenceId = l.ReferenceId,
                Note = l.Note,
                CreatedAt = l.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return PagedData<ReputationHistoryItemDto>.From(items, safePage, safeLimit, totalCount);
    }

    public async Task<ReputationPolicyResponse> GetReputationPolicyAsync(CancellationToken cancellationToken = default)
    {
        var configs = await _dbContext.SystemConfigs
            .AsNoTracking()
            .ToDictionaryAsync(c => c.ConfigKey, c => c.ConfigValue, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var enforcementEnabled = !configs.TryGetValue("FEATURE_REPUTATION_ENFORCEMENT", out var enfVal)
            || !bool.TryParse(enfVal, out var parsedEnf)
            || parsedEnf;

        var initialScore = ParseConfigInt(configs, "DEFAULT_REPUTATION_SCORE", ReputationRules.DefaultInitialScore);
        var minBid = ParseConfigInt(configs, "AUCTION_MIN_REPUTATION_BID", ReputationRules.DefaultMinScoreToBid);
        var minSell = ParseConfigInt(configs, "AUCTION_MIN_REPUTATION_CREATE", ReputationRules.DefaultMinScoreToSell);
        var paymentTimeout = ParseConfigInt(configs, "PAYMENT_TIMEOUT_PENALTY_SCORE", Math.Abs(ReputationRules.PaymentTimeoutPenalty));
        var sellerCancel = ParseConfigInt(configs, "SELLER_CANCEL_PENALTY_SCORE", Math.Abs(ReputationRules.SellerCancelPenalty));
        var orderCompletedReward = ParseConfigInt(configs, "ORDER_COMPLETED_REWARD_SCORE", ReputationRules.OrderCompletedReward);
        var cooldownDays = ParseConfigInt(configs, "ANTI_FARMING_COOLDOWN_DAYS", ReputationRules.AntiFarmingCooldownDays);

        var tiers = new List<TrustLevelTierDto>
        {
            new() { Level = TrustLevel.Untrusted.ToEnumString(), MinScore = 0, MaxScore = 39, Description = "Restricted account: bidding and selling privileges are suspended." },
            new() { Level = TrustLevel.Probation.ToEnumString(), MinScore = 40, MaxScore = 49, Description = "Probation status: allowed to bid as buyer, but restricted from creating seller auctions." },
            new() { Level = TrustLevel.Standard.ToEnumString(), MinScore = 50, MaxScore = 69, Description = "Standard trust level: full bidding and selling privileges enabled." },
            new() { Level = TrustLevel.Good.ToEnumString(), MinScore = 70, MaxScore = 89, Description = "Trusted member with a consistent positive trading track record." },
            new() { Level = TrustLevel.Excellent.ToEnumString(), MinScore = 90, MaxScore = 100, Description = "Top-tier member eligible for priority badges and platform perks." }
        };

        return new ReputationPolicyResponse
        {
            EnforcementEnabled = enforcementEnabled,
            InitialScore = initialScore,
            MinScore = ReputationRules.MinScore,
            MaxScore = ReputationRules.MaxScore,
            MinScoreToBid = minBid,
            MinScoreToSell = minSell,
            PaymentTimeoutPenalty = -Math.Abs(paymentTimeout),
            SellerCancelPenalty = -Math.Abs(sellerCancel),
            OrderCompletedReward = Math.Abs(orderCompletedReward),
            AntiFarmingCooldownDays = cooldownDays,
            TrustLevels = tiers
        };
    }

    public async Task<PagedData<MyPenaltyItemDto>> GetMyPenaltiesAsync(
        Guid userId,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var (safePage, safeLimit) = PaginationConstants.Normalize(page, pageSize);
        var now = DateTimeOffset.UtcNow;

        var query = _dbContext.UserPenalties
            .AsNoTracking()
            .Where(p => p.UserId == userId);

        var totalCount = await query.CountAsync(cancellationToken);

        var rawItems = await query
            .OrderByDescending(p => p.IssuedAt)
            .Skip((safePage - 1) * safeLimit)
            .Take(safeLimit)
            .ToListAsync(cancellationToken);

        var items = rawItems.Select(p => new MyPenaltyItemDto
        {
            PenaltyId = p.Id,
            TargetRole = p.TargetRole.ToEnumString(),
            ViolationType = p.ViolationType.ToEnumString(),
            Reason = p.Reason,
            PenaltyApplied = p.PenaltyApplied.ToEnumString(),
            PointsDeducted = p.PointsDeducted,
            Status = p.Status.ToEnumString(),
            PolicyClauseCode = p.PolicyClauseCode,
            IssuedAt = p.IssuedAt,
            ExpiresAt = p.ExpiresAt,
            IsCurrentlyActive = p.Status == PenaltyStatus.Active && (p.ExpiresAt == null || p.ExpiresAt > now)
        }).ToList();

        return PagedData<MyPenaltyItemDto>.From(items, safePage, safeLimit, totalCount);
    }

    public async Task<AdjustReputationResponse> AdjustReputationAsync(
        Guid targetUserId,
        AdjustReputationRequest request,
        Guid adminId,
        CancellationToken cancellationToken = default)
    {
        var delta = request.EffectiveDelta;
        if (delta == 0 || delta < -50 || delta > 50)
        {
            throw new DomainException("VAL_INVALID_INPUT", "Score delta must be a non-zero integer between -50 and 50.", 400);
        }

        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 10)
        {
            throw new DomainException("VAL_INVALID_INPUT", "Adjustment reason must be at least 10 characters long.", 400);
        }

        var user = await _dbContext.Users
            .Include(u => u.ReputationScore)
            .FirstOrDefaultAsync(u => u.Id == targetUserId, cancellationToken);

        if (user == null)
        {
            throw new DomainException("USER_NOT_FOUND", "User account was not found for the specified identifier.", 404);
        }

        var repScore = user.ReputationScore;
        if (repScore == null)
        {
            repScore = ReputationScore.CreateDefault(targetUserId);
            _dbContext.ReputationScores.Add(repScore);
        }

        var effectiveTrack = request.TargetRole == TargetRole.Seller ? TargetRole.Seller : TargetRole.Buyer;
        var prevTrustLevel = effectiveTrack == TargetRole.Seller
            ? repScore.SellerTrustLevel.ToEnumString()
            : repScore.TrustLevel.ToEnumString();

        var (oldScore, newScore, appliedDelta) = repScore.ApplyScoreChange(
            effectiveTrack,
            delta,
            isPenalty: delta < 0);

        var newTrustLevel = effectiveTrack == TargetRole.Seller
            ? repScore.SellerTrustLevel.ToEnumString()
            : repScore.TrustLevel.ToEnumString();

        var now = DateTimeOffset.UtcNow;

        _dbContext.ReputationLogs.Add(new ReputationLog
        {
            Id = Guid.NewGuid(),
            UserId = targetUserId,
            TargetRole = effectiveTrack,
            ChangeAmount = appliedDelta,
            ScoreAfter = newScore,
            Reason = ReputationChangeReason.ManualAdjustment,
            ReferenceType = request.ReferenceType ?? ReferenceType.System,
            ReferenceId = request.ReferenceId,
            Note = request.Reason.Trim(),
            CreatedBy = adminId,
            CreatedAt = now,
            UpdatedAt = now
        });

        _dbContext.AddAuditLog(
            action: AuditAction.ReputationAdjust,
            entityName: nameof(ReputationScore),
            entityId: targetUserId.ToString(),
            oldValues: new { TargetRole = effectiveTrack.ToEnumString(), Score = oldScore, TrustLevel = prevTrustLevel },
            newValues: new { TargetRole = effectiveTrack.ToEnumString(), Score = newScore, TrustLevel = newTrustLevel, Reason = request.Reason.Trim() },
            userId: adminId.ToString(),
            changedColumns: new[] { effectiveTrack == TargetRole.Seller ? nameof(ReputationScore.SellerScore) : nameof(ReputationScore.CurrentScore) });

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "[ReputationService] Adjusted {TargetRole} score for UserId '{UserId}' from {OldScore} to {NewScore} by Admin '{AdminId}'",
            effectiveTrack, targetUserId, oldScore, newScore, adminId);

        return new AdjustReputationResponse
        {
            UserId = targetUserId,
            TargetRole = effectiveTrack.ToEnumString(),
            PreviousScore = oldScore,
            NewScore = newScore,
            AppliedDelta = appliedDelta,
            PreviousTrustLevel = prevTrustLevel,
            NewTrustLevel = newTrustLevel,
            Reason = request.Reason.Trim(),
            AdjustedBy = adminId,
            AdjustedAt = now
        };
    }

    private async Task<int> GetConfigIntAsync(string key, int defaultValue, CancellationToken cancellationToken)
    {
        var config = await _dbContext.SystemConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ConfigKey == key, cancellationToken);

        if (config != null && int.TryParse(config.ConfigValue, out var parsed))
        {
            return parsed;
        }

        return defaultValue;
    }

    private static int ParseConfigInt(IReadOnlyDictionary<string, string> configs, string key, int defaultValue)
    {
        if (configs.TryGetValue(key, out var val) && int.TryParse(val, out var parsed))
        {
            return parsed;
        }

        return defaultValue;
    }
}
