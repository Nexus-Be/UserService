namespace User.Application.Services.Seller;

using System;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexus.Contracts.Events;
using User.Application.Interfaces;
using User.Domain.Entities;
using User.Domain.Enums;
using User.Domain.Exceptions;

public sealed class SellerService : ISellerService
{
    private readonly IUserDbContext _dbContext;
    private readonly IRedisOtpService _otpService;
    private readonly IPublishEndpoint? _publishEndpoint;
    private readonly ILogger<SellerService> _logger;

    public SellerService(
        IUserDbContext dbContext,
        IRedisOtpService otpService,
        IPublishEndpoint? publishEndpoint,
        ILogger<SellerService> logger)
    {
        _dbContext = dbContext;
        _otpService = otpService;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task<SellerRegistrationResponseDto> RegisterSellerAsync(
        Guid userId,
        CreateSellerRegistrationDto dto,
        CancellationToken cancellationToken = default)
    {
        if (dto == null)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Registration payload is required.");
        }

        if (userId == Guid.Empty)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "User identifier is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.ShopName) || dto.ShopName.Trim().Length < 3 || dto.ShopName.Trim().Length > 100)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_SHOP_NAME", "Shop name must be between 3 and 100 characters.");
        }

        if (string.IsNullOrWhiteSpace(dto.BusinessEmail) || !dto.BusinessEmail.Contains('@') || !dto.BusinessEmail.Contains('.'))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_EMAIL", "Email address format is invalid.");
        }

        if (string.IsNullOrWhiteSpace(dto.Hotline))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_HOTLINE", "Hotline is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.IdentityCardNumber))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_IDENTITY_CARD", "Identity card number is required.");
        }

        var normalizedShopName = dto.ShopName.Trim();
        var normalizedEmail = dto.BusinessEmail.Trim().ToLowerInvariant();

        // 1. Kiểm tra tài khoản người dùng
        var user = await _dbContext.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Include(u => u.SellerProfile)
            .Include(u => u.ReputationScore)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null)
        {
            _logger.LogWarning("[SELLER ONBOARDING] User '{UserId}' not found.", userId);
            throw new DomainException("USER_NOT_FOUND", "User account not found.");
        }

        if (user.Status == UserStatus.Banned)
        {
            _logger.LogWarning("[SELLER ONBOARDING] User '{UserId}' is banned.", userId);
            throw new DomainException("USER_BANNED", "User account has been banned.");
        }

        if (user.Status != UserStatus.Active)
        {
            _logger.LogWarning("[SELLER ONBOARDING] User '{UserId}' is not active (Status: {Status}).", userId, user.Status);
            throw new DomainException("USER_NOT_ACTIVE", "User account must be active to register as a seller.");
        }

        // 2. Kiểm tra nếu đã là Seller
        var hasSellerRole = user.UserRoles.Any(ur => ur.Role?.RoleCode == RoleType.Seller);
        var hasApprovedProfile = user.SellerProfile != null && user.SellerProfile.Status == SellerProfileStatus.Approved;
        if (hasSellerRole || hasApprovedProfile)
        {
            _logger.LogWarning("[SELLER ONBOARDING] User '{UserId}' is already a seller.", userId);
            throw new DomainException("ALREADY_SELLER", "User is already registered as a seller.");
        }

        // 3. Kiểm tra hồ sơ chờ duyệt
        if (user.SellerProfile != null && user.SellerProfile.Status == SellerProfileStatus.Pending)
        {
            _logger.LogWarning("[SELLER ONBOARDING] User '{UserId}' already has a pending registration.", userId);
            throw new DomainException("REQUEST_ALREADY_PENDING", "You already have a pending seller registration request.");
        }

        // 4. Rào chắn điểm uy tín (>= 50)
        if (user.ReputationScore != null && user.ReputationScore.CurrentScore < 50)
        {
            _logger.LogWarning("[SELLER ONBOARDING] User '{UserId}' reputation score ({Score}) is below 50.", userId, user.ReputationScore.CurrentScore);
            throw new DomainException("REPUTATION_TOO_LOW", "User reputation score must be at least 50 to register as a seller.");
        }

        // 5. Kiểm tra trùng tên Shop
        var isShopNameTaken = await _dbContext.SellerProfiles
            .AnyAsync(s => s.ShopName.ToLower() == normalizedShopName.ToLower() && s.UserId != userId, cancellationToken);

        if (isShopNameTaken)
        {
            _logger.LogWarning("[SELLER ONBOARDING] Shop name '{ShopName}' is already taken.", normalizedShopName);
            throw new DomainException("SHOP_NAME_TAKEN", "Shop name is already taken.");
        }

        // 6. Xử lý địa chỉ kho lấy hàng GHN
        var pickupAddress = !string.IsNullOrWhiteSpace(dto.PickupAddress)
            ? dto.PickupAddress.Trim()
            : !string.IsNullOrWhiteSpace(dto.StreetAddress)
                ? dto.StreetAddress.Trim()
                : !string.IsNullOrWhiteSpace(dto.WarehouseAddress?.StreetAddress)
                    ? dto.WarehouseAddress.StreetAddress.Trim()
                    : "Pending Update";

        var provinceId = dto.ProvinceId
            ?? dto.WarehouseAddress?.ProvinceId
            ?? 0;

        var districtId = dto.DistrictId
            ?? dto.WarehouseAddress?.DistrictId
            ?? 0;

        var wardCode = !string.IsNullOrWhiteSpace(dto.WardCode)
            ? dto.WardCode.Trim()
            : !string.IsNullOrWhiteSpace(dto.WarehouseAddress?.WardCode)
                ? dto.WarehouseAddress.WardCode.Trim()
                : "00000";

        // Đồng bộ số CCCD vào user nếu chưa có
        if (string.IsNullOrWhiteSpace(user.IdentifyNumber))
        {
            user.IdentifyNumber = dto.IdentityCardNumber.Trim();
            user.UpdatedAt = DateTimeOffset.UtcNow;
        }

        // 7. Tạo hoặc tái sử dụng bản ghi SellerProfile (tránh vi phạm Unique Index IX_SellerProfiles_UserId nếu trước đó bị Rejected)
        SellerProfile sellerProfile;
        if (user.SellerProfile != null)
        {
            sellerProfile = user.SellerProfile;
            sellerProfile.ShopName = normalizedShopName;
            sellerProfile.BusinessEmail = normalizedEmail;
            sellerProfile.Hotline = dto.Hotline.Trim();
            sellerProfile.PickupAddress = pickupAddress;
            sellerProfile.ProvinceId = provinceId;
            sellerProfile.DistrictId = districtId;
            sellerProfile.WardCode = wardCode;
            sellerProfile.Status = SellerProfileStatus.Pending;
            sellerProfile.RejectionReason = null;
            sellerProfile.ReviewedBy = null;
            sellerProfile.ReviewedAt = null;
            sellerProfile.UpdatedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            sellerProfile = new SellerProfile
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ShopName = normalizedShopName,
                BusinessEmail = normalizedEmail,
                Hotline = dto.Hotline.Trim(),
                PickupAddress = pickupAddress,
                ProvinceId = provinceId,
                DistrictId = districtId,
                WardCode = wardCode,
                Status = SellerProfileStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _dbContext.SellerProfiles.Add(sellerProfile);
        }

        // 8. Sinh mã OTP xác minh email doanh nghiệp
        var otpCode = Random.Shared.Next(100000, 999999).ToString();
        await _otpService.SaveOtpAsync(
            normalizedEmail,
            otpCode,
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(15),
            purpose: "seller_registration"
        );

        _logger.LogInformation("[SELLER OTP] BusinessEmail: '{Email}' | CODE: >>> {OtpCode} <<< | Valid: 5 mins", normalizedEmail, otpCode);

        // 9. Bắn sự kiện SendOtpEmailIntegrationEvent vào Outbox (Publish trước SaveChangesAsync)
        if (_publishEndpoint != null)
        {
            try
            {
                var fullName = $"{user.FirstName} {user.LastName}".Trim();
                await _publishEndpoint.Publish(new SendOtpEmailIntegrationEvent(
                    normalizedEmail,
                    fullName,
                    otpCode,
                    5
                ), cancellationToken);

                _logger.LogInformation("Enqueued SendOtpEmailIntegrationEvent for seller business email '{Email}'.", normalizedEmail);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to enqueue SendOtpEmailIntegrationEvent for seller registration. OTP: {Otp}", otpCode);
            }
        }

        // 10. Commit vào CSDL PostgreSQL
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[SELLER ONBOARDING] Successfully created seller registration for User '{UserId}', Shop '{ShopName}'.", userId, normalizedShopName);

        return new SellerRegistrationResponseDto(
            RegistrationId: sellerProfile.Id.ToString(),
            Status: "PENDING",
            SubmittedAt: sellerProfile.CreatedAt
        );
    }

    public async Task<PagedResultDto<AdminSellerRegistrationItemDto>> GetSellerRegistrationsAsync(
        int page,
        int limit,
        string? status,
        CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        limit = limit < 1 ? 20 : (limit > 100 ? 100 : limit);

        var query = _dbContext.SellerProfiles
            .AsNoTracking()
            .Include(s => s.User)
                .ThenInclude(u => u!.ReputationScore)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (Enum.TryParse<SellerProfileStatus>(status.Trim(), true, out var parsedStatus))
            {
                query = query.Where(s => s.Status == parsedStatus);
            }
        }

        var total = await query.CountAsync(cancellationToken);

        var profiles = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var items = profiles.Select(s => new AdminSellerRegistrationItemDto(
            RegistrationId: s.Id.ToString(),
            UserId: s.UserId.ToString(),
            ShopName: s.ShopName,
            BusinessEmail: s.BusinessEmail,
            Hotline: s.Hotline,
            IdentityCardNumber: s.User?.IdentifyNumber ?? string.Empty,
            WarehouseAddress: s.PickupAddress,
            PickupAddress: s.PickupAddress,
            ReputationScore: s.User?.ReputationScore?.CurrentScore ?? 50,
            Status: s.Status.ToString().ToUpperInvariant(),
            CreatedAt: s.CreatedAt
        )).ToList();

        return new PagedResultDto<AdminSellerRegistrationItemDto>(items, total, page, limit);
    }

    public async Task<ApproveSellerRegistrationResponseDto> ApproveSellerRegistrationAsync(
        Guid registrationId,
        Guid? reviewerId,
        ApproveSellerRegistrationDto? dto,
        CancellationToken cancellationToken = default)
    {
        if (registrationId == Guid.Empty)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Registration identifier is required.");
        }

        var sellerProfile = await _dbContext.SellerProfiles
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Id == registrationId, cancellationToken);

        if (sellerProfile == null)
        {
            sellerProfile = await _dbContext.SellerProfiles
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.UserId == registrationId, cancellationToken);
        }

        if (sellerProfile == null)
        {
            _logger.LogWarning("[SELLER APPROVAL] Seller registration '{RegistrationId}' not found.", registrationId);
            throw new DomainException("REGISTRATION_NOT_FOUND", "Seller registration not found.");
        }

        if (sellerProfile.Status == SellerProfileStatus.Approved)
        {
            _logger.LogWarning("[SELLER APPROVAL] Registration '{RegistrationId}' is already approved.", registrationId);
            throw new DomainException("ALREADY_APPROVED", "Seller registration has already been approved.");
        }

        // 1. Tự động cấp Role SELLER cho User (vẫn giữ role Buyer)
        var sellerRole = await _dbContext.Roles
            .FirstOrDefaultAsync(r => r.RoleCode == RoleType.Seller, cancellationToken);

        if (sellerRole != null)
        {
            var alreadyHasRole = await _dbContext.UserRoles
                .AnyAsync(ur => ur.UserId == sellerProfile.UserId && ur.RoleId == sellerRole.Id, cancellationToken);

            if (!alreadyHasRole)
            {
                _dbContext.UserRoles.Add(new UserRole
                {
                    Id = Guid.NewGuid(),
                    UserId = sellerProfile.UserId,
                    RoleId = sellerRole.Id,
                    AssignedAt = DateTimeOffset.UtcNow,
                    AssignedBy = reviewerId
                });
                _logger.LogInformation("[SELLER APPROVAL] Assigned role 'Seller' to UserId: '{UserId}'.", sellerProfile.UserId);
            }
        }

        // 2. Cập nhật hồ sơ SellerProfile
        sellerProfile.Status = SellerProfileStatus.Approved;
        sellerProfile.ReviewedBy = reviewerId;
        sellerProfile.ReviewedAt = DateTimeOffset.UtcNow;
        sellerProfile.UpdatedAt = DateTimeOffset.UtcNow;

        // 3. Ghi vết kiểm toán (AuditLog)
        _dbContext.AuditLogs.Add(new AuditLog
        {
            ServiceName = "UserService",
            Action = AuditAction.SellerApprove,
            EntityName = nameof(SellerProfile),
            EntityId = sellerProfile.Id.ToString(),
            UserId = reviewerId?.ToString(),
            NewValues = System.Text.Json.JsonSerializer.Serialize(new { status = "APPROVED", note = dto?.Note }),
            CreatedAt = DateTimeOffset.UtcNow
        });

        // 4. Bắn sự kiện SellerApprovedIntegrationEvent vào Outbox (Publish trước SaveChangesAsync)
        if (_publishEndpoint != null)
        {
            try
            {
                await _publishEndpoint.Publish(new SellerApprovedIntegrationEvent(
                    sellerProfile.Id,
                    sellerProfile.UserId,
                    sellerProfile.ShopName,
                    DateTimeOffset.UtcNow
                ), cancellationToken);

                _logger.LogInformation("Enqueued SellerApprovedIntegrationEvent to Outbox for seller '{SellerId}'.", sellerProfile.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to enqueue SellerApprovedIntegrationEvent for seller '{SellerId}'.", sellerProfile.Id);
            }
        }

        // 5. Commit vào PostgreSQL
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[SELLER APPROVAL] Successfully approved seller '{SellerId}' by Reviewer '{ReviewerId}'.", sellerProfile.Id, reviewerId);

        return new ApproveSellerRegistrationResponseDto(
            RegistrationId: sellerProfile.Id.ToString(),
            UserId: sellerProfile.UserId.ToString(),
            Status: "APPROVED",
            ApprovedAt: sellerProfile.ReviewedAt.Value
        );
    }

    public async Task<RejectSellerRegistrationResponseDto> RejectSellerRegistrationAsync(
        Guid registrationId,
        Guid? reviewerId,
        RejectSellerRegistrationDto dto,
        CancellationToken cancellationToken = default)
    {
        if (registrationId == Guid.Empty)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Registration identifier is required.");
        }

        if (dto == null || string.IsNullOrWhiteSpace(dto.RejectionReason))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Rejection reason is required.");
        }

        var sellerProfile = await _dbContext.SellerProfiles
            .FirstOrDefaultAsync(s => s.Id == registrationId, cancellationToken);

        if (sellerProfile == null)
        {
            sellerProfile = await _dbContext.SellerProfiles
                .FirstOrDefaultAsync(s => s.UserId == registrationId, cancellationToken);
        }

        if (sellerProfile == null)
        {
            _logger.LogWarning("[SELLER REJECTION] Seller registration '{RegistrationId}' not found.", registrationId);
            throw new DomainException("REGISTRATION_NOT_FOUND", "Seller registration not found.");
        }

        var reason = dto.RejectionReason.Trim();
        sellerProfile.Status = SellerProfileStatus.Rejected;
        sellerProfile.RejectionReason = reason;
        sellerProfile.ReviewedBy = reviewerId;
        sellerProfile.ReviewedAt = DateTimeOffset.UtcNow;
        sellerProfile.UpdatedAt = DateTimeOffset.UtcNow;

        _dbContext.AuditLogs.Add(new AuditLog
        {
            ServiceName = "UserService",
            Action = AuditAction.SellerReject,
            EntityName = nameof(SellerProfile),
            EntityId = sellerProfile.Id.ToString(),
            UserId = reviewerId?.ToString(),
            NewValues = System.Text.Json.JsonSerializer.Serialize(new { status = "REJECTED", reason }),
            CreatedAt = DateTimeOffset.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[SELLER REJECTION] Rejected seller registration '{SellerId}' with reason: '{Reason}'.", sellerProfile.Id, reason);

        return new RejectSellerRegistrationResponseDto(
            RegistrationId: sellerProfile.Id.ToString(),
            UserId: sellerProfile.UserId.ToString(),
            Status: "REJECTED",
            RejectedAt: sellerProfile.ReviewedAt.Value,
            RejectionReason: reason
        );
    }
}
