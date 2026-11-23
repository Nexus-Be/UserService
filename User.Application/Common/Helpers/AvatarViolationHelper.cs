namespace User.Application.Common.Helpers;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using User.Application.Common.Extensions;
using User.Application.Interfaces;
using User.Domain.Entities;
using User.Domain.Enums;
using User.Domain.Exceptions;

public sealed record AvatarPenaltyOutcome(
    int DeductedPoints,
    int NewScore,
    int ViolationCount,
    bool AutoBanned);

public static class AvatarViolationHelper
{
    public const string PolicyCode = "INAPPROPRIATE_AVATAR";
    public const string ReviewPolicyCode = "INAPPROPRIATE_REVIEW_IMAGE";
    public const int PenaltyPointsPerViolation = 10;
    public const int AutoBanThresholdCount = 3;

    public static async Task ModerateAndEnforceAsync(
        IUserDbContext dbContext,
        IImageModerationService? moderationService,
        Guid userId,
        Stream content,
        string fileName,
        string contentType,
        string policyCode,
        string errorCode,
        string itemLabel,
        CancellationToken cancellationToken = default,
        IRedisOtpService? redisOtpService = null)
    {
        if (moderationService == null)
        {
            return;
        }

        if (content.CanSeek) content.Position = 0;
        var moderation = await moderationService.ModerateAsync(content, fileName, contentType, cancellationToken);
        if (!moderation.IsAllowed)
        {
            var user = await dbContext.Users
                .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, cancellationToken);

            if (user != null)
            {
                var outcome = await ApplyPenaltyAsync(
                    dbContext,
                    user,
                    moderation.Reason ?? $"Blocked inappropriate {policyCode.ToLowerInvariant()} ({moderation.ViolationCategory}).",
                    actorId: null,
                    now: DateTimeOffset.UtcNow,
                    policyCode: policyCode,
                    cancellationToken: cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);

                if (outcome.AutoBanned && redisOtpService != null)
                {
                    try
                    {
                        await redisOtpService.RevokeUserTokensAsync(userId, TimeSpan.FromDays(30));
                    }
                    catch
                    {
                        // Best-effort Redis token revocation; DB ban & RefreshToken revocation already committed.
                    }
                }

                var message = outcome.AutoBanned
                    ? $"{itemLabel} violates community standards. Your account has been banned due to repeated violations."
                    : $"{itemLabel} violates community standards. {outcome.DeductedPoints} reputation points were deducted (violation {outcome.ViolationCount}/{AutoBanThresholdCount}).";
                throw new BusinessRuleValidationException(errorCode, message);
            }

            throw new BusinessRuleValidationException(errorCode, $"{itemLabel} violates community standards.");
        }

        if (content.CanSeek) content.Position = 0;
    }

    public static async Task<AvatarPenaltyOutcome> ApplyPenaltyAsync(
        IUserDbContext dbContext,
        Domain.Entities.User user,
        string reason,
        Guid? actorId,
        DateTimeOffset now,
        string policyCode = PolicyCode,
        CancellationToken cancellationToken = default)
    {
        var repScore = await dbContext.ReputationScores
            .FirstOrDefaultAsync(r => r.UserId == user.Id, cancellationToken);

        if (repScore == null)
        {
            repScore = ReputationScore.CreateDefault(user.Id);
            dbContext.ReputationScores.Add(repScore);
        }

        var (_, newScore, appliedDelta) = repScore.ApplyScoreChange(
            TargetRole.Buyer,
            -PenaltyPointsPerViolation,
            isPenalty: true);

        var effectiveReason = string.IsNullOrWhiteSpace(reason)
            ? "Violated community image standards."
            : reason.Trim();

        dbContext.ReputationLogs.Add(new ReputationLog
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TargetRole = TargetRole.Buyer,
            ChangeAmount = appliedDelta,
            ScoreAfter = newScore,
            Reason = ReputationChangeReason.ReportPenalty,
            ReferenceType = ReferenceType.System,
            ReferenceId = user.Id.ToString(),
            Note = effectiveReason,
            CreatedBy = actorId ?? user.Id,
            CreatedAt = now,
            UpdatedAt = now
        });

        var previousViolations = await dbContext.UserPenalties
            .AsNoTracking()
            .CountAsync(
                p => p.UserId == user.Id && (p.PolicyClauseCode == PolicyCode || p.PolicyClauseCode == ReviewPolicyCode),
                cancellationToken);

        var violationCount = previousViolations + 1;
        var shouldAutoBan = violationCount >= AutoBanThresholdCount || newScore <= 0;

        dbContext.UserPenalties.Add(new UserPenalty
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TargetRole = TargetRole.Account,
            ViolationType = PenaltyViolationType.Other,
            PenaltyApplied = shouldAutoBan ? PenaltyType.BanAccount : PenaltyType.DeductPoints,
            PointsDeducted = Math.Abs(appliedDelta),
            PolicyClauseCode = policyCode,
            Reason = effectiveReason,
            Status = PenaltyStatus.Active,
            IssuedAt = now,
            CreatedBy = actorId ?? user.Id,
            CreatedAt = now,
            UpdatedAt = now
        });

        if (shouldAutoBan && user.Status != UserStatus.Banned)
        {
            var oldStatus = user.Status.ToString();
            var banReason = $"Automatically banned account after {violationCount} inappropriate image upload violations.";
            user.Status = UserStatus.Banned;
            user.BannedAt = now;
            user.BannedBy = actorId;
            user.BanReason = banReason;
            user.UpdatedAt = now;

            var activeTokens = await dbContext.RefreshTokens
                .Where(t => t.UserId == user.Id && !t.IsRevoked)
                .ToListAsync(cancellationToken);

            foreach (var token in activeTokens)
            {
                token.IsRevoked = true;
                token.RevokedAt = now;
                token.UpdatedAt = now;
            }

            dbContext.AddAuditLog(
                action: AuditAction.UserBan,
                entityName: "User",
                entityId: user.Id.ToString(),
                oldValues: new Dictionary<string, object?> { ["Status"] = oldStatus },
                newValues: new Dictionary<string, object?> { ["Status"] = nameof(UserStatus.Banned), ["BanReason"] = banReason, ["PolicyCode"] = policyCode },
                userId: actorId?.ToString(),
                changedColumns: new[] { "Status", "BannedAt", "BannedBy", "BanReason" });
        }

        return new AvatarPenaltyOutcome(
            DeductedPoints: Math.Abs(appliedDelta),
            NewScore: newScore,
            ViolationCount: violationCount,
            AutoBanned: shouldAutoBan);
    }
}
