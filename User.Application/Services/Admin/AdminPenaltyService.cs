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

    // =========================================================================================
    // SECTION 1: TRA CỨU VÀ QUẢN LÝ ÁN PHẠT KỶ LUẬT (SANCTIONS MANAGEMENT & ENFORCEMENT)
    // =========================================================================================

    /// <summary>
    /// Tra cứu danh sách các biên bản xử phạt vi phạm có phân trang và bộ lọc.
    /// Hỗ trợ lọc theo Người dùng, Vai trò mục tiêu (Buyer/Seller/Account), và Trạng thái án phạt.
    /// </summary>
    /// <param name="page">Số trang hiện tại (bắt đầu từ 1).</param>
    /// <param name="limit">Số bản ghi mỗi trang (mặc định 20, tối đa 100).</param>
    /// <param name="userId">Lọc theo ID người dùng chịu phạt.</param>
    /// <param name="targetRole">Lọc theo vai trò mục tiêu: Buyer, Seller, hoặc Account.</param>
    /// <param name="status">Lọc theo trạng thái: Active, Expired, Revoked.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu.</param>
    /// <returns>Danh sách biên bản xử phạt phân trang kèm metadata.</returns>
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

    /// <summary>
    /// Xem thông tin chi tiết một biên bản xử phạt vi phạm theo ID.
    /// Bao gồm lý do vi phạm, điều khoản chính sách, bằng chứng chứng minh, và thông tin người ra quyết định.
    /// </summary>
    /// <param name="penaltyId">ID biên bản xử phạt.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu.</param>
    /// <returns>Chi tiết biên bản vi phạm.</returns>
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

    /// <summary>
    /// Thu hồi án phạt vi phạm trước thời hạn bởi Quản trị viên (Admin).
    /// Khôi phục trạng thái hoạt động của tài khoản hoặc gian hàng tùy theo TargetRole bị phạt.
    /// Tự động ghi nhận AuditLog tập trung.
    /// </summary>
    /// <param name="penaltyId">ID biên bản xử phạt cần thu hồi.</param>
    /// <param name="dto">Lý do thu hồi án phạt.</param>
    /// <param name="adminId">ID quản trị viên thực hiện.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu.</param>
    /// <returns>Xác nhận thu hồi án phạt và trạng thái hoạt động đã khôi phục.</returns>
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

    // =========================================================================================
    // SECTION 2: KIỂM TRA QUYỀN HÀNH VI THỜI GIAN THỰC CHO LIÊN SERVICE (INTER-SERVICE GATEKEEPER)
    // =========================================================================================

    /// <summary>
    /// [Internal Gatekeeper] Đánh giá thời gian thực người dùng có quyền thực hiện hành vi hay không (Buy, Sell, Login, Comment).
    /// Kiểm tra trạng thái tài khoản UserStatus, trạng thái gian hàng ShopStatus, và các án phạt Active trong UserPenalties.
    /// Dành cho API Gateway và OrderService gọi trực tiếp để chặn hành vi vi phạm tức thì.
    /// </summary>
    /// <param name="userId">ID người dùng cần kiểm tra.</param>
    /// <param name="action">Hành vi mục tiêu (Buy, Sell, Login, Comment).</param>
    /// <param name="cancellationToken">Token hủy yêu cầu.</param>
    /// <returns>Kết quả cho phép hoặc bị cấm kèm lý do và danh sách các án phạt đang hiệu lực.</returns>
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
