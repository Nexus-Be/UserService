namespace User.Application.Services.Seller;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using User.Application.Common.Extensions;
using User.Application.Common;
using User.Application.Common.Models;
using User.Application.Interfaces;
using User.Domain.Entities;
using User.Domain.Enums;
using User.Domain.Exceptions;

public sealed class SellerService : ISellerService
{
    private readonly IUserDbContext _dbContext;
    private readonly IRedisOtpService _otpService;
    private readonly IEmailNotificationClient? _emailNotificationClient;
    private readonly ILogger<SellerService> _logger;

    public SellerService(
        IUserDbContext dbContext,
        IRedisOtpService otpService,
        IEmailNotificationClient? emailNotificationClient,
        ILogger<SellerService> logger)
    {
        _dbContext = dbContext;
        _otpService = otpService;
        _emailNotificationClient = emailNotificationClient;
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

        // 1. Validate user account
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

        // 2. Check if already a Seller
        var hasSellerRole = user.UserRoles.Any(ur => ur.Role?.RoleCode == RoleType.Seller);
        var hasApprovedProfile = user.SellerProfile != null && user.SellerProfile.Status == SellerProfileStatus.Approved;
        if (hasSellerRole || hasApprovedProfile)
        {
            _logger.LogWarning("[SELLER ONBOARDING] User '{UserId}' is already a seller.", userId);
            throw new DomainException("ALREADY_SELLER", "User is already registered as a seller.");
        }

        // 3. Check for pending registration
        if (user.SellerProfile != null && user.SellerProfile.Status == SellerProfileStatus.Pending)
        {
            _logger.LogWarning("[SELLER ONBOARDING] User '{UserId}' already has a pending registration.", userId);
            throw new DomainException("REQUEST_ALREADY_PENDING", "You already have a pending seller registration request.");
        }

        // 4. Check reputation threshold (>= 50)
        if (user.ReputationScore != null && user.ReputationScore.CurrentScore < 50)
        {
            _logger.LogWarning("[SELLER ONBOARDING] User '{UserId}' reputation score ({Score}) is below 50.", userId, user.ReputationScore.CurrentScore);
            throw new DomainException("REPUTATION_TOO_LOW", "User reputation score must be at least 50 to register as a seller.");
        }

        // 5. Check duplicate shop name
        var isShopNameTaken = await _dbContext.SellerProfiles
            .AnyAsync(s => s.ShopName.ToLower() == normalizedShopName.ToLower() && s.UserId != userId, cancellationToken);

        if (isShopNameTaken)
        {
            _logger.LogWarning("[SELLER ONBOARDING] Shop name '{ShopName}' is already taken.", normalizedShopName);
            throw new DomainException("SHOP_NAME_TAKEN", "Shop name is already taken.");
        }

        // 6. Resolve warehouse pickup address
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

        // Sync identity card number to user if missing
        if (string.IsNullOrWhiteSpace(user.IdentifyNumber))
        {
            user.IdentifyNumber = dto.IdentityCardNumber.Trim();
            user.UpdatedAt = DateTimeOffset.UtcNow;
        }

        // 7. Create or reuse SellerProfile entity to avoid unique index violation
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

        // 8. Generate business email verification OTP
        var otpCode = Random.Shared.Next(100000, 999999).ToString();
        await _otpService.SaveOtpAsync(
            normalizedEmail,
            otpCode,
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(15),
            purpose: "seller_registration"
        );

        _logger.LogInformation("[SELLER OTP] BusinessEmail: '{Email}' | CODE: >>> {OtpCode} <<< | Valid: 5 mins", normalizedEmail, otpCode);

        // 9. Dispatch OTP email via Notification Service REST client
        if (_emailNotificationClient != null)
        {
            try
            {
                var fullName = $"{user.FirstName} {user.LastName}".Trim();
                await _emailNotificationClient.SendOtpEmailAsync(
                    normalizedEmail,
                    fullName,
                    otpCode,
                    5,
                    cancellationToken);

                _logger.LogInformation("Dispatched OTP email via REST client for seller business email '{Email}'.", normalizedEmail);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to dispatch OTP email for seller registration. OTP: {Otp}", otpCode);
            }
        }

        // 10. Commit to database
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[SELLER ONBOARDING] Successfully created seller registration for User '{UserId}', Shop '{ShopName}'.", userId, normalizedShopName);

        return new SellerRegistrationResponseDto(
            RegistrationId: sellerProfile.Id.ToString(),
            Status: "PENDING",
            SubmittedAt: sellerProfile.CreatedAt
        );
    }

    public async Task<PagedData<AdminSellerRegistrationItemDto>> GetSellerRegistrationsAsync(
        int page,
        int limit,
        string? status,
        CancellationToken cancellationToken = default)
    {
        var (safePage, safeLimit) = PaginationConstants.Normalize(page, limit);
        page = safePage;
        limit = safeLimit;

        var query = _dbContext.SellerProfiles
            .AsNoTracking()
            .Include(s => s.User)
                .ThenInclude(u => u!.ReputationScore)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (status.TryToEnum<SellerProfileStatus>(out var parsedStatus))
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

        return new PagedData<AdminSellerRegistrationItemDto>(items, PaginationMetadata.Create(page, limit, total));
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

        // 1. Assign Seller role to user
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

        // 2. Update SellerProfile status
        sellerProfile.Status = SellerProfileStatus.Approved;
        sellerProfile.ReviewedBy = reviewerId;
        sellerProfile.ReviewedAt = DateTimeOffset.UtcNow;
        sellerProfile.UpdatedAt = DateTimeOffset.UtcNow;

        // 3. Record audit log
        _dbContext.AddAuditLog(
            action: AuditAction.SellerApprove,
            entityName: nameof(SellerProfile),
            entityId: sellerProfile.Id.ToString(),
            newValues: new { status = "APPROVED", note = dto?.Note },
            userId: reviewerId?.ToString());

        // 4. Commit to database
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

        _dbContext.AddAuditLog(
            action: AuditAction.SellerReject,
            entityName: nameof(SellerProfile),
            entityId: sellerProfile.Id.ToString(),
            newValues: new { status = "REJECTED", reason },
            userId: reviewerId?.ToString());

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

    public async Task<SellerOwnProfileResponseDto> GetSellerOwnProfileAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "User identifier is required.");
        }

        var sellerProfile = await _dbContext.SellerProfiles
            .AsNoTracking()
            .Include(sp => sp.User)
                .ThenInclude(u => u!.ReputationScore)
            .Include(sp => sp.User)
                .ThenInclude(u => u!.ReviewsReceived)
            .FirstOrDefaultAsync(sp => sp.UserId == userId, cancellationToken);

        if (sellerProfile == null)
        {
            _logger.LogWarning("[SELLER PROFILE] Seller profile not found for UserId: '{UserId}'.", userId);
            throw new DomainException("SELLER_NOT_FOUND", "Seller profile was not found for the current user.");
        }

        var reviews = sellerProfile.User?.ReviewsReceived?.Where(r => !r.IsDisabled).ToList();
        double ratingAvg = 5.0;
        if (reviews != null && reviews.Count > 0)
        {
            ratingAvg = Math.Round(reviews.Average(r => r.Rating), 2);
        }

        var reputationScore = sellerProfile.User?.ReputationScore?.CurrentScore ?? 50;
        var trustLevel = sellerProfile.User?.ReputationScore?.TrustLevel.ToString() ?? TrustLevel.Standard.ToString();

        return new SellerOwnProfileResponseDto(
            SellerId: sellerProfile.Id.ToString(),
            ShopId: sellerProfile.Id.ToString(),
            UserId: sellerProfile.UserId.ToString(),
            ShopName: sellerProfile.ShopName,
            AvatarUrl: sellerProfile.AvatarUrl,
            BusinessEmail: sellerProfile.BusinessEmail,
            Hotline: sellerProfile.Hotline,
            Description: null,
            PickupAddress: sellerProfile.PickupAddress,
            ProvinceId: sellerProfile.ProvinceId,
            DistrictId: sellerProfile.DistrictId,
            WardCode: sellerProfile.WardCode,
            Status: sellerProfile.Status.ToString().ToUpperInvariant(),
            RatingAvg: ratingAvg,
            ReputationScore: reputationScore,
            TrustLevel: trustLevel,
            CreatedAt: sellerProfile.CreatedAt,
            UpdatedAt: sellerProfile.UpdatedAt
        );
    }

    public async Task<SellerOwnProfileUpdatedResponseDto> UpdateSellerOwnProfileAsync(
        Guid userId,
        UpdateSellerOwnProfileDto dto,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "User identifier is required.");
        }

        if (dto == null)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Update payload is required.");
        }

        var sellerProfile = await _dbContext.SellerProfiles
            .Include(sp => sp.User)
            .FirstOrDefaultAsync(sp => sp.UserId == userId, cancellationToken);

        if (sellerProfile == null)
        {
            _logger.LogWarning("[SELLER PROFILE UPDATE] Seller profile not found for UserId: '{UserId}'.", userId);
            throw new DomainException("SELLER_NOT_FOUND", "Seller profile was not found for the current user.");
        }

        if (sellerProfile.User != null && sellerProfile.User.Status == UserStatus.Banned)
        {
            _logger.LogWarning("[SELLER PROFILE UPDATE] User '{UserId}' is banned.", userId);
            throw new DomainException("USER_BANNED", "User account has been banned.");
        }

        // 1. Update ShopName if provided
        if (!string.IsNullOrWhiteSpace(dto.ShopName))
        {
            var trimmedShopName = dto.ShopName.Trim();
            if (trimmedShopName.Length < 3 || trimmedShopName.Length > 100)
            {
                throw new BusinessRuleValidationException("VAL_INVALID_SHOP_NAME", "Shop name must be between 3 and 100 characters.");
            }

            if (!string.Equals(sellerProfile.ShopName, trimmedShopName, StringComparison.OrdinalIgnoreCase))
            {
                var isTaken = await _dbContext.SellerProfiles
                    .AnyAsync(s => s.ShopName.ToLower() == trimmedShopName.ToLower() && s.UserId != userId, cancellationToken);

                if (isTaken)
                {
                    _logger.LogWarning("[SELLER PROFILE UPDATE] Shop name '{ShopName}' is already taken.", trimmedShopName);
                    throw new DomainException("SHOP_NAME_TAKEN", "Shop name is already taken.");
                }

                sellerProfile.ShopName = trimmedShopName;
            }
        }

        // 2. Update Hotline if provided
        if (!string.IsNullOrWhiteSpace(dto.Hotline))
        {
            sellerProfile.Hotline = dto.Hotline.Trim();
        }

        // 3. Update AvatarUrl if provided
        if (!string.IsNullOrWhiteSpace(dto.AvatarUrl))
        {
            sellerProfile.AvatarUrl = dto.AvatarUrl.Trim();
        }

        // 4. Update BusinessEmail if provided
        if (!string.IsNullOrWhiteSpace(dto.BusinessEmail))
        {
            var normalizedEmail = dto.BusinessEmail.Trim().ToLowerInvariant();
            if (!normalizedEmail.Contains('@') || !normalizedEmail.Contains('.'))
            {
                throw new BusinessRuleValidationException("VAL_INVALID_EMAIL", "Email address format is invalid.");
            }
            sellerProfile.BusinessEmail = normalizedEmail;
        }

        // 5. Update warehouse pickup address fields (PickupAddress / StreetAddress / Province / District / Ward)
        var newAddress = !string.IsNullOrWhiteSpace(dto.PickupAddress)
            ? dto.PickupAddress.Trim()
            : !string.IsNullOrWhiteSpace(dto.StreetAddress)
                ? dto.StreetAddress.Trim()
                : null;

        if (!string.IsNullOrWhiteSpace(newAddress))
        {
            sellerProfile.PickupAddress = newAddress;
        }

        if (dto.ProvinceId.HasValue && dto.ProvinceId.Value > 0)
        {
            sellerProfile.ProvinceId = dto.ProvinceId.Value;
        }

        if (dto.DistrictId.HasValue && dto.DistrictId.Value > 0)
        {
            sellerProfile.DistrictId = dto.DistrictId.Value;
        }

        if (!string.IsNullOrWhiteSpace(dto.WardCode))
        {
            sellerProfile.WardCode = dto.WardCode.Trim();
        }

        sellerProfile.UpdatedAt = DateTimeOffset.UtcNow;

        // 6. Record audit log
        _dbContext.AddAuditLog(
            action: AuditAction.UserUpdate,
            entityName: nameof(SellerProfile),
            entityId: sellerProfile.Id.ToString(),
            newValues: new
            {
                shopName = sellerProfile.ShopName,
                hotline = sellerProfile.Hotline,
                avatarUrl = sellerProfile.AvatarUrl,
                businessEmail = sellerProfile.BusinessEmail,
                pickupAddress = sellerProfile.PickupAddress,
                provinceId = sellerProfile.ProvinceId,
                districtId = sellerProfile.DistrictId,
                wardCode = sellerProfile.WardCode
            },
            userId: userId.ToString());

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[SELLER PROFILE UPDATE] Successfully updated seller profile '{SellerId}' for User '{UserId}'.", sellerProfile.Id, userId);

        return new SellerOwnProfileUpdatedResponseDto(
            ShopId: sellerProfile.Id.ToString(),
            SellerId: sellerProfile.Id.ToString(),
            ShopName: sellerProfile.ShopName,
            UpdatedAt: sellerProfile.UpdatedAt,
            Message: "Seller profile updated successfully."
        );
    }
}
