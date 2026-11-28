namespace User.Application.Services.AuctionViolations;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using User.Application.Common.Extensions;
using User.Application.Common.Models;
using User.Application.Interfaces;
using User.Domain.Constants;
using User.Domain.Entities;
using User.Domain.Enums;
using User.Domain.Exceptions;

public sealed class AuctionViolationAdminService : IAuctionViolationAdminService
{
    private readonly IUserDbContext _dbContext;

    public AuctionViolationAdminService(IUserDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<AdminAuctionViolationsResponse> GetAuctionViolationsAsync(
        Guid userId,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100)
        {
            throw new DomainException(
                "VAL_INVALID_INPUT",
                "Page must be at least 1 and page size must be between 1 and 100.",
                400);
        }

        var userExists = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == userId && u.Status != UserStatus.Deleted, cancellationToken);

        if (!userExists)
        {
            throw new DomainException("USER_NOT_FOUND", "User account was not found for the specified identifier.", 404);
        }

        var profile = await _dbContext.UserAuctionProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var restriction = profile.ResolveActiveRestriction(now);

        var query = _dbContext.AuctionViolations
            .AsNoTracking()
            .Where(v => v.UserId == userId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(v => v.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(v => new AdminAuctionViolationItemDto
            {
                ViolationId = v.Id,
                ViolationType = v.ViolationType,
                AuctionId = v.AuctionId,
                OrderId = v.OrderId,
                StreakAfter = v.NewStreak,
                ReputationDelta = v.ReputationAfter - v.ReputationBefore,
                DepositForfeitedAmount = v.DepositForfeitedAmount,
                Sanction = v.SanctionType,
                SanctionUntil = v.SanctionUntil,
                Reason = v.Reason,
                CreatedAt = v.CreatedAt,
                Source = v.Source
            })
            .ToListAsync(cancellationToken);

        return new AdminAuctionViolationsResponse
        {
            UserId = userId,
            CurrentStreak = profile?.ViolationStreak ?? 0,
            AuctionRestriction = new AdminAuctionRestrictionDto
            {
                IsRestricted = restriction.IsRestricted,
                Type = restriction.Type,
                Until = restriction.Until,
                Reason = restriction.Reason
            },
            Violations = PagedData<AdminAuctionViolationItemDto>.From(items, page, pageSize, totalCount)
        };
    }

    public async Task<AdjustAuctionViolationResponse> AdjustAuctionViolationAsync(
        Guid userId,
        AdjustAuctionViolationRequest request,
        Guid adminId,
        string? ipAddress = null,
        string? userAgent = null,
        string? traceId = null,
        CancellationToken cancellationToken = default)
    {
        if (!request.Action.HasValue)
        {
            throw new DomainException("VAL_INVALID_INPUT", "Adjustment action is required.", 400);
        }

        var trimmedReason = request.Reason?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedReason))
        {
            throw new DomainException("VAL_INVALID_INPUT", "Adjustment reason is required.", 400);
        }

        var now = DateTimeOffset.UtcNow;
        var action = request.Action.Value;

        switch (action)
        {
            case AuctionAdminAdjustAction.SetStreak:
                if (!request.Value.HasValue || request.Value.Value < 0)
                {
                    throw new DomainException("VAL_INVALID_INPUT", "Streak value must be a non-negative integer.", 400);
                }
                break;

            case AuctionAdminAdjustAction.ApplyRestriction:
                if (!request.Until.HasValue || request.Until.Value <= now)
                {
                    throw new DomainException("VAL_INVALID_INPUT", "Restriction expiration timestamp must be in the future.", 400);
                }
                break;

            case AuctionAdminAdjustAction.Suspend:
                if (request.Until.HasValue && request.Until.Value <= now)
                {
                    throw new DomainException("VAL_INVALID_INPUT", "Suspension expiration timestamp must be in the future.", 400);
                }
                break;
        }

        var targetUser = await _dbContext.Users
            .Include(u => u.Role)
            .Include(u => u.ReputationScore)
            .FirstOrDefaultAsync(u => u.Id == userId && u.Status != UserStatus.Deleted, cancellationToken);

        if (targetUser is null)
        {
            throw new DomainException("USER_NOT_FOUND", "User account was not found for the specified identifier.", 404);
        }

        if (targetUser.Role?.RoleCode == RoleType.Admin)
        {
            throw new DomainException(
                "CANNOT_RESTRICT_ADMIN",
                "Cannot adjust auction violations or apply restrictions to an administrator account.",
                409);
        }

        var profile = await _dbContext.GetOrCreateAuctionProfileAsync(userId, now, cancellationToken);

        var oldStreak = profile.ViolationStreak;
        var oldEffectiveRestricted = profile.IsCurrentlyRestricted(now);
        var oldRestrictionType = oldEffectiveRestricted ? profile.RestrictionType : AuctionRestrictionType.None;
        var oldUntil = oldEffectiveRestricted ? profile.RestrictionUntil : null;

        var newStreak = oldStreak;
        var newRestrictionType = oldRestrictionType;
        var newUntil = oldUntil;
        var newReason = oldEffectiveRestricted ? profile.RestrictionReason : null;
        var sanctionType = AuctionSanctionType.None;

        switch (action)
        {
            case AuctionAdminAdjustAction.ResetStreak:
                newStreak = 0;
                break;

            case AuctionAdminAdjustAction.SetStreak:
                newStreak = request.Value!.Value;
                break;

            case AuctionAdminAdjustAction.ApplyRestriction:
                newRestrictionType = AuctionRestrictionType.TempRestricted;
                newUntil = request.Until;
                newReason = trimmedReason;
                sanctionType = AuctionSanctionType.TempRestricted;
                break;

            case AuctionAdminAdjustAction.LiftRestriction:
                newRestrictionType = AuctionRestrictionType.None;
                newUntil = null;
                newReason = null;
                break;

            case AuctionAdminAdjustAction.Suspend:
                newRestrictionType = AuctionRestrictionType.Suspended;
                newUntil = request.Until;
                newReason = trimmedReason;
                sanctionType = AuctionSanctionType.Suspended;
                break;
        }

        if (newStreak == oldStreak &&
            newRestrictionType == oldRestrictionType &&
            newUntil == oldUntil)
        {
            throw new DomainException(
                "NOTHING_TO_CHANGE",
                "The requested adjustment produces no change to the user's auction profile.",
                409);
        }

        profile.ViolationStreak = newStreak;
        profile.RestrictionType = newRestrictionType;
        profile.RestrictionUntil = newUntil;
        profile.RestrictionReason = newReason;
        profile.UpdatedAt = now;
        profile.UpdatedBy = adminId;

        var currentRep = targetUser.ReputationScore?.CurrentScore ?? ReputationRules.DefaultInitialScore;

        _dbContext.AuctionViolations.Add(new AuctionViolation
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ViolationType = null,
            AuctionId = null,
            OrderId = null,
            OccurredAt = now,
            DepositForfeitedAmount = 0m,
            Source = AuctionViolationSource.Admin,
            PreviousStreak = oldStreak,
            NewStreak = newStreak,
            ReputationBefore = currentRep,
            ReputationAfter = currentRep,
            SanctionType = sanctionType,
            SanctionUntil = newUntil,
            Reason = trimmedReason,
            CreatedBy = adminId,
            CreatedAt = now,
            UpdatedAt = now
        });

        var beforeSnapshot = new AdjustAuctionViolationSnapshotDto
        {
            Streak = oldStreak,
            Restriction = oldRestrictionType == AuctionRestrictionType.None ? null : oldRestrictionType,
            RestrictionUntil = oldUntil
        };

        var afterSnapshot = new AdjustAuctionViolationSnapshotDto
        {
            Streak = newStreak,
            Restriction = newRestrictionType == AuctionRestrictionType.None ? null : newRestrictionType,
            RestrictionUntil = newUntil
        };

        var auditLog = _dbContext.AddAuditLog(
            action: AuditAction.AuctionViolationAdjust,
            entityName: nameof(UserAuctionProfile),
            entityId: profile.Id.ToString(),
            oldValues: beforeSnapshot,
            newValues: new { afterSnapshot.Streak, afterSnapshot.Restriction, afterSnapshot.RestrictionUntil, Reason = trimmedReason, Action = action.ToString() },
            userId: adminId.ToString(),
            ipAddress: ipAddress,
            userAgent: userAgent,
            traceId: traceId);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new AdjustAuctionViolationResponse
        {
            UserId = userId,
            Before = beforeSnapshot,
            After = afterSnapshot,
            AuditId = auditLog.Id
        };
    }
}
