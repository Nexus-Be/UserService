namespace User.Application.Services.Admin;

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
using User.Domain.Entities;
using User.Domain.Enums;
using User.Domain.Exceptions;

public sealed class AdminPenaltyService : IAdminPenaltyService
{
    private readonly IUserDbContext _dbContext;
    private readonly ILogger<AdminPenaltyService> _logger;
    private readonly IRedisOtpService? _redisOtpService;

    public AdminPenaltyService(
        IUserDbContext dbContext,
        ILogger<AdminPenaltyService> logger,
        IRedisOtpService? redisOtpService = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _redisOtpService = redisOtpService;
    }

    public async Task<PagedData<AdminPenaltyListItemDto>> GetPenaltiesAsync(
        int page = 1,
        int limit = 20,
        Guid? userId = null,
        TargetRole? targetRole = null,
        PenaltyStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (limit < 1 || limit > 100) limit = 20;

        var query = _dbContext.UserPenalties
            .Include(p => p.User)
            .AsNoTracking();

        if (userId.HasValue && userId.Value != Guid.Empty)
        {
            query = query.Where(p => p.UserId == userId.Value);
        }

        if (targetRole.HasValue)
        {
            query = query.Where(p => p.TargetRole == targetRole.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status.Value);
        }

        var totalItems = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(p => p.IssuedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(p => new AdminPenaltyListItemDto(
                p.Id.ToString(),
                p.UserId.ToString(),
                p.User != null ? p.User.Email : null,
                p.TargetRole,
                p.PenaltyApplied,
                p.ViolationType,
                p.Status,
                p.Reason,
                p.PolicyClauseCode,
                p.PointsDeducted,
                p.IssuedAt,
                p.ExpiresAt
            ))
            .ToListAsync(cancellationToken);

        return PagedData<AdminPenaltyListItemDto>.From(items, page, limit, totalItems);
    }

    public async Task<AdminPenaltyDetailDto> GetPenaltyDetailAsync(
        Guid penaltyId,
        CancellationToken cancellationToken = default)
    {
        if (penaltyId == Guid.Empty)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Penalty ID is required.");
        }

        var penalty = await _dbContext.UserPenalties
            .Include(p => p.User)
            .Include(p => p.CreatedByUser)
            .Include(p => p.RevokedByUser)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == penaltyId, cancellationToken);

        if (penalty == null)
        {
            throw new DomainException("PENALTY_NOT_FOUND", "Penalty record was not found.", 404);
        }

        var fullName = penalty.User != null ? $"{penalty.User.FirstName} {penalty.User.LastName}".Trim() : null;

        return new AdminPenaltyDetailDto(
            Id: penalty.Id.ToString(),
            UserId: penalty.UserId.ToString(),
            UserEmail: penalty.User?.Email,
            UserFullName: fullName,
            TargetRole: penalty.TargetRole,
            PenaltyApplied: penalty.PenaltyApplied,
            ViolationType: penalty.ViolationType,
            Status: penalty.Status,
            Reason: penalty.Reason,
            PolicyClauseCode: penalty.PolicyClauseCode,
            EvidenceUrls: penalty.EvidenceUrls ?? new List<string>(),
            PointsDeducted: penalty.PointsDeducted,
            IssuedAt: penalty.IssuedAt,
            ExpiresAt: penalty.ExpiresAt,
            CreatedBy: penalty.CreatedBy?.ToString(),
            RevokedBy: penalty.RevokedBy?.ToString(),
            RevokedAt: penalty.RevokedAt,
            RevokeReason: penalty.RevokeReason
        );
    }

    public async Task<AdminPenaltyRevokedResponseDto> RevokePenaltyAsync(
        Guid penaltyId,
        RevokePenaltyDto dto,
        Guid adminId,
        CancellationToken cancellationToken = default)
    {
        if (penaltyId == Guid.Empty)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Penalty ID is required.");
        }

        if (dto == null || string.IsNullOrWhiteSpace(dto.Reason))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Revocation reason is required.");
        }

        var trimmedReason = dto.Reason.Trim();
        if (trimmedReason.Length > 1000)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Revocation reason cannot exceed 1000 characters.");
        }

        var penalty = await _dbContext.UserPenalties
            .Include(p => p.User)
                .ThenInclude(u => u!.SellerProfile)
            .FirstOrDefaultAsync(p => p.Id == penaltyId, cancellationToken);

        if (penalty == null)
        {
            throw new DomainException("PENALTY_NOT_FOUND", "Penalty record was not found.", 404);
        }

        if (penalty.Status == PenaltyStatus.Revoked)
        {
            throw new DomainException("PENALTY_ALREADY_REVOKED", "Penalty has already been revoked.", 400);
        }

        var now = DateTimeOffset.UtcNow;
        penalty.Revoke(adminId, trimmedReason);

        var restoredState = "PenaltyRevoked";
        var user = penalty.User;

        // Restore operational projections if no other conflicting active penalties exist
        if (user != null)
        {
            var otherActivePenalties = await _dbContext.UserPenalties
                .Where(p => p.UserId == user.Id && p.Id != penalty.Id && p.Status == PenaltyStatus.Active && (p.ExpiresAt == null || p.ExpiresAt > now))
                .ToListAsync(cancellationToken);

            if (penalty.TargetRole == TargetRole.Seller)
            {
                var hasOtherSellerPenalty = otherActivePenalties.Any(p => p.TargetRole == TargetRole.Seller || p.TargetRole == TargetRole.Account);
                if (!hasOtherSellerPenalty && user.Status != UserStatus.Banned && user.SellerProfile != null && user.SellerProfile.Status == SellerProfileStatus.Suspended)
                {
                    user.SellerProfile.Reactivate();
                    restoredState = "SellerProfileReactivated";
                }
            }
            else if (penalty.TargetRole == TargetRole.Account)
            {
                var hasOtherAccountPenalty = otherActivePenalties.Any(p => p.TargetRole == TargetRole.Account);
                if (!hasOtherAccountPenalty && user.Status == UserStatus.Banned)
                {
                    user.Status = user.EmailVerifiedAt.HasValue ? UserStatus.Active : UserStatus.PendingVerification;
                    user.BannedAt = null;
                    user.BannedBy = null;
                    user.BanReason = null;
                    user.UpdatedAt = now;

                    if (user.SellerProfile != null && user.SellerProfile.Status == SellerProfileStatus.Suspended)
                    {
                        var hasOtherSellerPenalty = otherActivePenalties.Any(p => p.TargetRole == TargetRole.Seller);
                        if (!hasOtherSellerPenalty)
                        {
                            user.SellerProfile.Reactivate();
                        }
                    }

                    restoredState = "AccountRestored";
                }
            }
        }

        _dbContext.AddAuditLog(
            action: AuditAction.UserUnban,
            entityName: "UserPenalty",
            entityId: penalty.Id.ToString(),
            oldValues: new Dictionary<string, object?> { ["Status"] = PenaltyStatus.Active.ToString() },
            newValues: new Dictionary<string, object?> { ["Status"] = PenaltyStatus.Revoked.ToString(), ["RevokeReason"] = trimmedReason, ["RevokedBy"] = adminId.ToString() },
            userId: adminId.ToString(),
            changedColumns: new[] { "Status", "RevokedBy", "RevokedAt", "RevokeReason" });

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (_redisOtpService != null)
        {
            try
            {
                await _redisOtpService.UnbanUserAsync(penalty.UserId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[PENALTY REVOKE] Error clearing Redis ban state for user '{UserId}'.", penalty.UserId);
            }
        }

        _logger.LogInformation("[PENALTY REVOKE] Revoked penalty '{PenaltyId}' for user '{UserId}' by admin '{AdminId}'. Restored state: {State}",
            penaltyId, penalty.UserId, adminId, restoredState);

        return new AdminPenaltyRevokedResponseDto(
            PenaltyId: penalty.Id.ToString(),
            UserId: penalty.UserId.ToString(),
            Status: penalty.Status,
            RevokedAt: now,
            RevokeReason: trimmedReason,
            RestoredOperationalState: restoredState
        );
    }

    public async Task<UserActionPermissionCheckResponseDto> CheckUserActionPermissionAsync(
        Guid userId,
        string action,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "User ID is required.");
        }

        var normalizedAction = action?.Trim().ToLowerInvariant() ?? string.Empty;
        var actionName = string.IsNullOrWhiteSpace(action) ? "general" : action.Trim();

        var user = await _dbContext.Users
            .Include(u => u.SellerProfile)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null || user.IsDeleted)
        {
            return new UserActionPermissionCheckResponseDto(
                UserId: userId.ToString(),
                Action: actionName,
                IsAllowed: false,
                Reason: "User was not found or is deleted.",
                ExpiresAt: null,
                ActiveRestrictions: new List<string> { "USER_NOT_FOUND" }
            );
        }

        var now = DateTimeOffset.UtcNow;

        // Query active penalties directly using PostgreSQL Filtered Index (WHERE "Status" = 0)
        var activePenalties = await _dbContext.UserPenalties
            .AsNoTracking()
            .Where(p => p.UserId == userId && p.Status == PenaltyStatus.Active && (p.ExpiresAt == null || p.ExpiresAt > now))
            .ToListAsync(cancellationToken);

        var restrictions = activePenalties.Select(p => $"{p.TargetRole}:{p.PenaltyApplied}").Distinct().ToList();
        var nearestExpiry = activePenalties.Where(p => p.ExpiresAt.HasValue).OrderBy(p => p.ExpiresAt).FirstOrDefault()?.ExpiresAt;

        if (user.Status == UserStatus.Banned)
        {
            return new UserActionPermissionCheckResponseDto(
                UserId: userId.ToString(),
                Action: actionName,
                IsAllowed: false,
                Reason: "User account is banned.",
                ExpiresAt: nearestExpiry,
                ActiveRestrictions: restrictions
            );
        }

        if (normalizedAction == "login")
        {
            var isAllowed = user.Status == UserStatus.Active || user.Status == UserStatus.PendingVerification;
            return new UserActionPermissionCheckResponseDto(
                UserId: userId.ToString(),
                Action: actionName,
                IsAllowed: isAllowed,
                Reason: isAllowed ? null : $"Account status '{user.Status}' does not permit login.",
                ExpiresAt: nearestExpiry,
                ActiveRestrictions: restrictions
            );
        }

        if (normalizedAction == "buy")
        {
            var hasBuyerSanction = activePenalties.Any(p =>
                (p.TargetRole == TargetRole.Buyer || p.TargetRole == TargetRole.Account) &&
                (p.PenaltyApplied == PenaltyType.Restriction || p.PenaltyApplied == PenaltyType.BanAccount));

            var canBuy = user.Status == UserStatus.Active && !hasBuyerSanction;
            var reason = canBuy ? null : "User has an active restriction prohibiting purchase actions.";
            return new UserActionPermissionCheckResponseDto(
                UserId: userId.ToString(),
                Action: actionName,
                IsAllowed: canBuy,
                Reason: reason,
                ExpiresAt: nearestExpiry,
                ActiveRestrictions: restrictions
            );
        }

        if (normalizedAction == "sell")
        {
            if (user.SellerProfile == null || user.SellerProfile.IsDeleted)
            {
                return new UserActionPermissionCheckResponseDto(
                    UserId: userId.ToString(),
                    Action: actionName,
                    IsAllowed: false,
                    Reason: "User does not have an approved seller profile.",
                    ExpiresAt: null,
                    ActiveRestrictions: new List<string> { "NO_SELLER_PROFILE" }
                );
            }

            if (user.SellerProfile.Status == SellerProfileStatus.Suspended)
            {
                return new UserActionPermissionCheckResponseDto(
                    UserId: userId.ToString(),
                    Action: actionName,
                    IsAllowed: false,
                    Reason: $"Seller profile is suspended. Reason: {user.SellerProfile.RejectionReason ?? "Operational violation"}",
                    ExpiresAt: nearestExpiry,
                    ActiveRestrictions: restrictions
                );
            }

            var hasSellerSanction = activePenalties.Any(p =>
                (p.TargetRole == TargetRole.Seller || p.TargetRole == TargetRole.Account) &&
                (p.PenaltyApplied == PenaltyType.Restriction || p.PenaltyApplied == PenaltyType.BanAccount));

            var canSell = user.Status == UserStatus.Active &&
                          user.SellerProfile.Status == SellerProfileStatus.Approved &&
                          !hasSellerSanction;

            return new UserActionPermissionCheckResponseDto(
                UserId: userId.ToString(),
                Action: actionName,
                IsAllowed: canSell,
                Reason: canSell ? null : "Seller has an active restriction prohibiting selling actions.",
                ExpiresAt: nearestExpiry,
                ActiveRestrictions: restrictions
            );
        }

        // Generic fallback check
        var generalAllowed = user.Status == UserStatus.Active;
        return new UserActionPermissionCheckResponseDto(
            UserId: userId.ToString(),
            Action: actionName,
            IsAllowed: generalAllowed,
            Reason: generalAllowed ? null : $"User status is '{user.Status}'.",
            ExpiresAt: nearestExpiry,
            ActiveRestrictions: restrictions
        );
    }
}
