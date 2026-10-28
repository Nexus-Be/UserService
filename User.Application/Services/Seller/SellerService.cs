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
}
