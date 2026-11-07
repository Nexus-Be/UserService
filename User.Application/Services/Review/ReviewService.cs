namespace User.Application.Services.Review;

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

public sealed class ReviewService : IReviewService
{
    private readonly IUserDbContext _dbContext;
    private readonly ILogger<ReviewService> _logger;

    public ReviewService(IUserDbContext dbContext, ILogger<ReviewService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<CreateReviewResponse> CreateReviewAsync(
        Guid reviewerId,
        CreateReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        if (reviewerId == request.RevieweeId)
        {
            throw new DomainException("SELF_REVIEW_NOT_ALLOWED", "You cannot submit a review for your own account.", 400);
        }

        if (request.Rating < 1 || request.Rating > 5)
        {
            throw new DomainException("VAL_INVALID_INPUT", "Rating must be between 1 and 5 stars.", 400);
        }

        if (string.IsNullOrWhiteSpace(request.ReferenceId))
        {
            throw new DomainException("VAL_INVALID_INPUT", "Reference ID is required.", 400);
        }

        var normalizedRefId = request.ReferenceId.Trim();

        var duplicateExists = await _dbContext.UserReviews
            .AsNoTracking()
            .AnyAsync(r =>
                r.ReviewerId == reviewerId &&
                r.ReferenceType == request.ReferenceType &&
                r.ReferenceId == normalizedRefId,
                cancellationToken);

        if (duplicateExists)
        {
            throw new DomainException("REVIEW_ALREADY_EXISTS", "A review has already been submitted for this transaction reference.", 409);
        }

        var reviewee = await _dbContext.Users
            .Include(u => u.ReputationScore)
            .Include(u => u.SellerProfile)
            .FirstOrDefaultAsync(u => u.Id == request.RevieweeId, cancellationToken);

        if (reviewee == null)
        {
            throw new DomainException("USER_NOT_FOUND", "The target user account was not found.", 404);
        }

        // Determine which track (Buyer vs Seller) is being evaluated:
        // - If Reviewer is Buyer -> Reviewee is Seller -> impacts SellerScore (TargetRole.Seller)
        // - If Reviewer is Seller -> Reviewee is Buyer -> impacts CurrentScore (TargetRole.Buyer)
        TargetRole targetTrack;
        if (request.ReviewerRole == ReviewerRole.Buyer)
        {
            if (reviewee.SellerProfile == null || reviewee.SellerProfile.Status != SellerProfileStatus.Approved)
            {
                throw new DomainException("INVALID_REVIEW_ROLE", "Target user does not have an active seller profile to receive buyer reviews.", 400);
            }
            targetTrack = TargetRole.Seller;
        }
        else
        {
            var reviewerHasSellerProfile = await _dbContext.SellerProfiles
                .AsNoTracking()
                .AnyAsync(sp => sp.UserId == reviewerId && sp.Status == SellerProfileStatus.Approved, cancellationToken);

            if (!reviewerHasSellerProfile)
            {
                throw new DomainException("INVALID_REVIEW_ROLE", "Only verified sellers can submit a review in the seller role.", 400);
            }
            targetTrack = TargetRole.Buyer;
        }

        var repScore = reviewee.ReputationScore;
        if (repScore == null)
        {
            repScore = ReputationScore.CreateDefault(reviewee.Id);
            _dbContext.ReputationScores.Add(repScore);
        }

        var rawDelta = ReputationRules.GetReviewScoreDelta(request.Rating);
        var effectiveDelta = rawDelta;
        var now = DateTimeOffset.UtcNow;

        // 7-Day Anti-Farming Cooldown:
        // Positive points (+1) from the same partner on the same track can only be earned once every 7 days.
        if (rawDelta > 0)
        {
            var cooldownDays = await GetCooldownDaysAsync(cancellationToken);
            var cutoff = now.AddDays(-cooldownDays);

            var recentlyRewardedFromSamePartner = await _dbContext.ReputationLogs
                .AsNoTracking()
                .AnyAsync(l =>
                    l.UserId == reviewee.Id &&
                    l.TargetRole == targetTrack &&
                    l.PartnerUserId == reviewerId &&
                    l.ChangeAmount > 0 &&
                    l.CreatedAt >= cutoff,
                    cancellationToken);

            if (recentlyRewardedFromSamePartner)
            {
                effectiveDelta = 0;
            }
        }

        var isPositiveReview = request.Rating >= 4;
        var isNegativeReview = request.Rating <= 2;

        var (_, newScore, appliedDelta) = repScore.ApplyScoreChange(
            targetTrack,
            effectiveDelta,
            isPositiveReview: isPositiveReview,
            isNegativeReview: isNegativeReview);

        var newTrustLevel = targetTrack == TargetRole.Seller
            ? repScore.SellerTrustLevel.ToEnumString()
            : repScore.TrustLevel.ToEnumString();

        var mediaUrls = request.MediaUrls?.Where(u => !string.IsNullOrWhiteSpace(u)).Take(5).ToList() ?? new List<string>();

        var review = new UserReview
        {
            Id = Guid.NewGuid(),
            ReviewerId = reviewerId,
            RevieweeId = reviewee.Id,
            ReviewerRole = request.ReviewerRole,
            ReferenceType = request.ReferenceType,
            ReferenceId = normalizedRefId,
            Rating = request.Rating,
            Comment = request.Comment?.Trim(),
            MediaUrls = mediaUrls,
            ScoreImpact = appliedDelta,
            IsDisputed = false,
            IsDisabled = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.UserReviews.Add(review);

        if (appliedDelta != 0)
        {
            _dbContext.ReputationLogs.Add(new ReputationLog
            {
                Id = Guid.NewGuid(),
                UserId = reviewee.Id,
                TargetRole = targetTrack,
                ChangeAmount = appliedDelta,
                ScoreAfter = newScore,
                Reason = appliedDelta > 0 ? ReputationChangeReason.PositiveReview : ReputationChangeReason.NegativeReview,
                ReferenceType = request.ReferenceType,
                ReferenceId = normalizedRefId,
                PartnerUserId = reviewerId,
                Note = $"Peer review ({request.Rating} stars)",
                CreatedBy = reviewerId,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "[ReviewService] Review '{ReviewId}' created by '{ReviewerId}' for '{RevieweeId}' ({TargetTrack} delta: {Delta})",
            review.Id, reviewerId, reviewee.Id, targetTrack, appliedDelta);

        return new CreateReviewResponse
        {
            ReviewId = review.Id,
            ReviewerId = reviewerId,
            RevieweeId = reviewee.Id,
            ReviewerRole = request.ReviewerRole.ToEnumString(),
            TargetRole = targetTrack.ToEnumString(),
            ReferenceType = request.ReferenceType.ToEnumString(),
            ReferenceId = normalizedRefId,
            Rating = request.Rating,
            Comment = review.Comment,
            MediaUrls = mediaUrls,
            ScoreImpact = appliedDelta,
            RevieweeNewScore = newScore,
            RevieweeNewTrustLevel = newTrustLevel,
            CreatedAt = now
        };
    }

    public async Task<PagedData<UserReviewItemDto>> GetUserReviewsAsync(
        Guid userId,
        int page = 1,
        int pageSize = 20,
        int? rating = null,
        ReviewerRole? reviewerRole = null,
        CancellationToken cancellationToken = default)
    {
        var userExists = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == userId, cancellationToken);

        if (!userExists)
        {
            throw new DomainException("USER_NOT_FOUND", "User account was not found for the specified identifier.", 404);
        }

        var (safePage, safeLimit) = PaginationConstants.Normalize(page, pageSize);

        var query = _dbContext.UserReviews
            .AsNoTracking()
            .Include(r => r.Reviewer)
            .Where(r => r.RevieweeId == userId && !r.IsDisabled);

        if (rating.HasValue)
        {
            query = query.Where(r => r.Rating == rating.Value);
        }

        if (reviewerRole.HasValue)
        {
            query = query.Where(r => r.ReviewerRole == reviewerRole.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var rawReviews = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((safePage - 1) * safeLimit)
            .Take(safeLimit)
            .ToListAsync(cancellationToken);

        var items = rawReviews.Select(r => new UserReviewItemDto
        {
            ReviewId = r.Id,
            ReviewerId = r.ReviewerId,
            ReviewerDisplayNameMasked = MaskDisplayName(r.Reviewer),
            ReviewerAvatarUrl = r.Reviewer?.AvatarUrl,
            ReviewerRole = r.ReviewerRole.ToEnumString(),
            Rating = r.Rating,
            Comment = r.Comment,
            MediaUrls = r.MediaUrls ?? new List<string>(),
            ReferenceType = r.ReferenceType.ToEnumString(),
            ReferenceId = r.ReferenceId,
            CreatedAt = r.CreatedAt
        }).ToList();

        return PagedData<UserReviewItemDto>.From(items, safePage, safeLimit, totalCount);
    }

    public async Task<HideReviewResponse> HideReviewAsync(
        Guid reviewId,
        HideReviewRequest request,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 10)
        {
            throw new DomainException("VAL_INVALID_INPUT", "Reason for hiding the review must be at least 10 characters long.", 400);
        }

        var review = await _dbContext.UserReviews
            .FirstOrDefaultAsync(r => r.Id == reviewId, cancellationToken);

        if (review == null)
        {
            throw new DomainException("REVIEW_NOT_FOUND", "Review was not found for the specified identifier.", 404);
        }

        if (review.IsDisabled)
        {
            throw new DomainException("REVIEW_ALREADY_HIDDEN", "This review has already been hidden by a moderator.", 409);
        }

        var repScore = await _dbContext.ReputationScores
            .FirstOrDefaultAsync(r => r.UserId == review.RevieweeId, cancellationToken);

        if (repScore == null)
        {
            repScore = ReputationScore.CreateDefault(review.RevieweeId);
            _dbContext.ReputationScores.Add(repScore);
        }

        var targetTrack = review.ReviewerRole == ReviewerRole.Buyer ? TargetRole.Seller : TargetRole.Buyer;
        var compensationDelta = -review.ScoreImpact;
        var now = DateTimeOffset.UtcNow;

        review.IsDisabled = true;
        review.HiddenBy = actorId;
        review.HiddenAt = now;
        review.HiddenReason = request.Reason.Trim();
        review.UpdatedAt = now;

        // Decrement review counters on the corresponding track
        if (targetTrack == TargetRole.Seller)
        {
            if (review.Rating >= 4 && repScore.SellerPositiveReviews > 0) repScore.SellerPositiveReviews--;
            if (review.Rating <= 2 && repScore.SellerNegativeReviews > 0) repScore.SellerNegativeReviews--;
        }
        else
        {
            if (review.Rating >= 4 && repScore.TotalPositiveReviews > 0) repScore.TotalPositiveReviews--;
            if (review.Rating <= 2 && repScore.TotalNegativeReviews > 0) repScore.TotalNegativeReviews--;
        }

        var (oldScore, newScore, appliedCompensation) = repScore.ApplyScoreChange(targetTrack, compensationDelta);
        var newTrustLevel = targetTrack == TargetRole.Seller
            ? repScore.SellerTrustLevel.ToEnumString()
            : repScore.TrustLevel.ToEnumString();

        if (appliedCompensation != 0)
        {
            _dbContext.ReputationLogs.Add(new ReputationLog
            {
                Id = Guid.NewGuid(),
                UserId = review.RevieweeId,
                TargetRole = targetTrack,
                ChangeAmount = appliedCompensation,
                ScoreAfter = newScore,
                Reason = ReputationChangeReason.DisputeCompensation,
                ReferenceType = review.ReferenceType,
                ReferenceId = review.ReferenceId,
                PartnerUserId = review.ReviewerId,
                Note = $"Compensated hidden review '{review.Id}': {request.Reason.Trim()}",
                CreatedBy = actorId,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        _dbContext.AddAuditLog(
            action: AuditAction.ReviewHide,
            entityName: nameof(UserReview),
            entityId: review.Id.ToString(),
            oldValues: new { IsDisabled = false, Score = oldScore },
            newValues: new { IsDisabled = true, Reason = request.Reason.Trim(), CompensatedDelta = appliedCompensation, NewScore = newScore },
            userId: actorId.ToString(),
            changedColumns: new[] { nameof(UserReview.IsDisabled), nameof(UserReview.HiddenBy), nameof(UserReview.HiddenAt), nameof(UserReview.HiddenReason) });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new HideReviewResponse
        {
            ReviewId = review.Id,
            RevieweeId = review.RevieweeId,
            IsDisabled = true,
            TargetRole = targetTrack.ToEnumString(),
            CompensatedScoreDelta = appliedCompensation,
            RevieweeNewScore = newScore,
            RevieweeNewTrustLevel = newTrustLevel,
            Reason = review.HiddenReason,
            HiddenBy = actorId,
            HiddenAt = now
        };
    }

    private async Task<int> GetCooldownDaysAsync(CancellationToken cancellationToken)
    {
        var config = await _dbContext.SystemConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ConfigKey == "ANTI_FARMING_COOLDOWN_DAYS", cancellationToken);

        if (config != null && int.TryParse(config.ConfigValue, out var days) && days > 0)
        {
            return days;
        }

        return ReputationRules.AntiFarmingCooldownDays;
    }

    private static string MaskDisplayName(User? user)
    {
        if (user == null)
        {
            return "u***r";
        }

        var full = $"{user.FirstName} {user.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(full))
        {
            full = user.Username ?? user.Email;
        }

        if (string.IsNullOrWhiteSpace(full) || full.Length <= 2)
        {
            return "u***r";
        }

        return $"{full[0]}***{full[^1]}";
    }
}
