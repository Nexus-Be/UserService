namespace User.Application.Services.Seller;

using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using User.Application.Common.Extensions;
using User.Application.Common;
using User.Application.Common.Models;
using User.Application.Interfaces;
using User.Application.Models;
using User.Domain.Entities;
using User.Domain.Enums;
using User.Domain.Exceptions;

public sealed class SellerService : ISellerService
{
    private static readonly string[] BannedShopKeywords = ["admin", "nexus", "official", "system", "support", "hotline", "shopee", "mall"];

    private readonly IUserDbContext _dbContext;
    private readonly IRedisOtpService _otpService;
    private readonly IEmailNotificationClient? _emailNotificationClient;
    private readonly IFulfillmentServiceClient _fulfillmentClient;
    private readonly ILogger<SellerService> _logger;

    public SellerService(
        IUserDbContext dbContext,
        IRedisOtpService otpService,
        IEmailNotificationClient? emailNotificationClient,
        IFulfillmentServiceClient fulfillmentClient,
        ILogger<SellerService> logger)
    {
        _dbContext = dbContext;
        _otpService = otpService;
        _emailNotificationClient = emailNotificationClient;
        _fulfillmentClient = fulfillmentClient;
        _logger = logger;
    }

    // =========================================================================================
    // SECTION 1: QUY TRÌNH ĐĂNG KÝ NGƯỜI BÁN VÀ XÁC THỰC EMAIL KYC (SELLER ONBOARDING & OTP)
    // Bao gồm: Đăng ký tạo bản nháp, xác thực OTP email, gửi lại OTP, và tra cứu trạng thái đơn nộp.
    // =========================================================================================

    /// <summary>
    /// Đăng ký hồ sơ nâng cấp Người bán từ tài khoản Buyer.
    /// Lưu ý: Dữ liệu được lưu tạm vào Redis Draft (TTL 15 phút), CHƯA ghi vào PostgreSQL để tránh dữ liệu rác.
    /// </summary>
    /// <param name="userId">ID người dùng đang đăng nhập.</param>
    /// <param name="dto">Dữ liệu đăng ký: Tên shop, Email kinh doanh, CCCD, Hotline, Địa chỉ kho lấy hàng.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu.</param>
    /// <returns>Dữ liệu xác nhận đã tạo bản nháp và thông báo gửi OTP thành công.</returns>
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

        var normalizedShopName = dto.ShopName.Trim();
        if (BannedShopKeywords.Any(k => normalizedShopName.Contains(k, StringComparison.OrdinalIgnoreCase)))
        {
            throw new BusinessRuleValidationException("VAL_BANNED_SHOP_NAME", "Shop name contains prohibited brand or system keywords.");
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

        // Resolve warehouse pickup address & GHN codes
        var pickupAddress = !string.IsNullOrWhiteSpace(dto.PickupAddress)
            ? dto.PickupAddress.Trim()
            : !string.IsNullOrWhiteSpace(dto.StreetAddress)
                ? dto.StreetAddress.Trim()
                : dto.WarehouseAddress?.StreetAddress?.Trim();

        var provinceId = dto.ProvinceId ?? dto.WarehouseAddress?.ProvinceId ?? 0;
        var districtId = dto.DistrictId ?? dto.WarehouseAddress?.DistrictId ?? 0;
        var wardCode = !string.IsNullOrWhiteSpace(dto.WardCode)
            ? dto.WardCode.Trim()
            : dto.WarehouseAddress?.WardCode?.Trim();

        if (string.IsNullOrWhiteSpace(pickupAddress))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_ADDRESS", "Pickup warehouse address is required.");
        }

        if (provinceId <= 0 || districtId <= 0 || string.IsNullOrWhiteSpace(wardCode))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_GHN_CODE", "Valid GHN ProvinceId, DistrictId, and WardCode are required.");
        }

        var normalizedEmail = dto.BusinessEmail.Trim().ToLowerInvariant();
        var normalizedIdentityCard = dto.IdentityCardNumber.Trim();

        // 1. Validate user account
        var user = await _dbContext.Users
            .Include(u => u.Role)
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
        var hasSellerRole = user.Role?.RoleCode == RoleType.Seller;
        var hasApprovedProfile = user.SellerProfile != null && user.SellerProfile.Status == SellerProfileStatus.Approved;
        if (hasSellerRole || hasApprovedProfile)
        {
            _logger.LogWarning("[SELLER ONBOARDING] User '{UserId}' is already a seller.", userId);
            throw new DomainException("ALREADY_SELLER", "User is already registered as a seller.");
        }

        // 3. Check for pending registration in DB
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

        // 5. Check duplicate shop name in DB
        var isShopNameTaken = await _dbContext.SellerProfiles
            .AnyAsync(s => s.ShopName.ToLower() == normalizedShopName.ToLower() && s.UserId != userId, cancellationToken);

        if (isShopNameTaken)
        {
            _logger.LogWarning("[SELLER ONBOARDING] Shop name '{ShopName}' is already taken.", normalizedShopName);
            throw new DomainException("SHOP_NAME_TAKEN", "Shop name is already taken.");
        }

        // 6. Check CCCD uniqueness (1 CCCD = 1 Shop)
        var isCccdTaken = await _dbContext.Users
            .AnyAsync(u => u.IdentifyNumber == normalizedIdentityCard && u.SellerProfile != null && u.Id != userId, cancellationToken);

        if (isCccdTaken)
        {
            _logger.LogWarning("[SELLER ONBOARDING] Identity card '{Cccd}' is already registered with another shop.", normalizedIdentityCard);
            throw new DomainException("IDENTITY_CARD_ALREADY_USED", "This identity card number has already been registered with another seller shop.");
        }

        // 7. Store draft in Redis with 15-minute TTL (NO DB write yet!)
        var draft = new SellerRegistrationDraft(
            UserId: userId,
            ShopName: normalizedShopName,
            BusinessEmail: normalizedEmail,
            Hotline: dto.Hotline.Trim(),
            IdentityCardNumber: normalizedIdentityCard,
            PickupAddress: pickupAddress,
            ProvinceId: provinceId,
            DistrictId: districtId,
            WardCode: wardCode,
            IdCardFrontUrl: dto.IdCardFrontUrl,
            IdCardBackUrl: dto.IdCardBackUrl,
            BusinessLicenseUrl: dto.BusinessLicenseUrl,
            AvatarUrl: dto.AvatarUrl
        );

        var draftJson = JsonSerializer.Serialize(draft);
        await _otpService.SaveDraftAsync($"user:seller_draft:{userId}", draftJson, TimeSpan.FromMinutes(15));
        _logger.LogInformation("[SELLER ONBOARDING] Draft stored in Redis for User '{UserId}', Shop '{ShopName}' (TTL 15m).", userId, normalizedShopName);

        // 8. Generate and save 6-digit OTP in Redis with 5-minute TTL
        var otpCode = Random.Shared.Next(100000, 999999).ToString();
        await _otpService.InvalidateExistingOtpsAsync(normalizedEmail, purpose: "seller_registration");
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

        return new SellerRegistrationResponseDto(
            BusinessEmail: normalizedEmail,
            Status: "OTP_VERIFICATION_REQUIRED",
            OtpExpiresInSeconds: 300,
            NextStep: "VERIFY_OTP_VIA_POST_/api/v1/sellers/otp/verify",
            SubmittedAt: DateTimeOffset.UtcNow
        );
    }

    /// <summary>
    /// Xác thực mã OTP email doanh nghiệp của đơn đăng ký Người bán.
    /// Khi OTP hợp lệ: Lấy bản nháp từ Redis, tạo bản ghi SellerProfile (Pending) vào PostgreSQL, ghi AuditLog.
    /// </summary>
    /// <param name="userId">ID người dùng xác thực.</param>
    /// <param name="dto">Dữ liệu xác thực: Email doanh nghiệp và Mã OTP 6 chữ số.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu.</param>
    /// <returns>Xác nhận xác thực OTP thành công và mã hồ sơ đăng ký mới tạo.</returns>
    public async Task<VerifySellerOtpResponseDto> VerifySellerRegistrationOtpAsync(
        Guid userId,
        VerifySellerOtpDto dto,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "User identifier is required.");
        }

        if (dto == null || string.IsNullOrWhiteSpace(dto.BusinessEmail) || string.IsNullOrWhiteSpace(dto.OtpCode))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Business email and OTP code are required.");
        }

        var normalizedEmail = dto.BusinessEmail.Trim().ToLowerInvariant();

        // 1. Check OTP in Redis
        var otpEntry = await _otpService.GetOtpAsync(normalizedEmail, purpose: "seller_registration");
        if (otpEntry != null && otpEntry.Status == OtpStatus.Consumed)
        {
            _logger.LogWarning("[SELLER OTP VERIFY] OTP already consumed for '{Email}'.", normalizedEmail);
            throw new DomainException("OTP_ALREADY_USED", "This OTP code has already been used.");
        }

        if (otpEntry == null || otpEntry.Status == OtpStatus.Expired || otpEntry.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            _logger.LogWarning("[SELLER OTP VERIFY] OTP expired or not found for '{Email}'.", normalizedEmail);
            throw new DomainException("OTP_EXPIRED", "OTP code has expired or does not exist. Please request a new code.");
        }

        if (!string.Equals(otpEntry.Code, dto.OtpCode.Trim(), StringComparison.Ordinal))
        {
            var attempts = await _otpService.IncrementOtpAttemptsAsync(normalizedEmail, TimeSpan.FromMinutes(5), purpose: "seller_registration");
            _logger.LogWarning("[SELLER OTP VERIFY] OTP mismatch for '{Email}'. Provided: '{Provided}', Expected: '{Expected}'. Attempt {Attempts}/5.",
                normalizedEmail, dto.OtpCode.Trim(), otpEntry.Code, attempts);

            if (attempts >= 5)
            {
                await _otpService.InvalidateExistingOtpsAsync(normalizedEmail, purpose: "seller_registration");
                await _otpService.GetAndDeleteDraftAsync($"user:seller_draft:{userId}");
                _logger.LogWarning("[SELLER OTP VERIFY] Maximum OTP verification attempts exceeded for '{Email}'. Invalidated OTP and draft.", normalizedEmail);
                throw new DomainException("OTP_MAX_ATTEMPTS_EXCEEDED", "Too many incorrect OTP attempts. The OTP code has been invalidated. Please submit registration again.");
            }

            throw new DomainException("OTP_INVALID", "Invalid verification code.");
        }

        // 2. Atomically retrieve & delete Draft from Redis (prevents concurrent double-submit)
        var draftJson = await _otpService.GetAndDeleteDraftAsync($"user:seller_draft:{userId}");
        if (string.IsNullOrWhiteSpace(draftJson))
        {
            _logger.LogWarning("[SELLER OTP VERIFY] Registration draft not found or already consumed for User '{UserId}'.", userId);
            throw new DomainException("DRAFT_NOT_FOUND", "Registration session expired or already processed. Please submit registration again.");
        }

        var draft = JsonSerializer.Deserialize<SellerRegistrationDraft>(draftJson);
        if (draft == null)
        {
            throw new DomainException("DRAFT_INVALID", "Invalid registration draft payload.");
        }

        // 3. Verify user exists and status
        var user = await _dbContext.Users
            .Include(u => u.SellerProfile)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null)
        {
            throw new DomainException("USER_NOT_FOUND", "User account not found.");
        }

        if (user.Status != UserStatus.Active)
        {
            throw new DomainException("USER_NOT_ACTIVE", "User account is not active.");
        }

        // 4. Anti-race condition: Check if ShopName was taken while user was typing OTP
        var isShopNameTaken = await _dbContext.SellerProfiles
            .AnyAsync(s => s.ShopName.ToLower() == draft.ShopName.ToLower() && s.UserId != userId, cancellationToken);

        if (isShopNameTaken)
        {
            throw new DomainException("SHOP_NAME_TAKEN", "Shop name is already taken.");
        }

        // 5. Sync IdentityCardNumber to User if missing
        if (string.IsNullOrWhiteSpace(user.IdentifyNumber))
        {
            user.IdentifyNumber = draft.IdentityCardNumber;
            user.UpdatedAt = DateTimeOffset.UtcNow;
        }

        // 6. Create or reuse SellerProfile (avoids 1-to-1 unique index conflict on re-registration)
        SellerProfile sellerProfile;
        if (user.SellerProfile != null)
        {
            sellerProfile = user.SellerProfile;
            sellerProfile.ShopName = draft.ShopName;
            sellerProfile.BusinessEmail = draft.BusinessEmail;
            sellerProfile.Hotline = draft.Hotline;
            sellerProfile.PickupAddress = draft.PickupAddress;
            sellerProfile.ProvinceId = draft.ProvinceId;
            sellerProfile.DistrictId = draft.DistrictId;
            sellerProfile.WardCode = draft.WardCode;
            sellerProfile.IdCardFrontUrl = draft.IdCardFrontUrl;
            sellerProfile.IdCardBackUrl = draft.IdCardBackUrl;
            sellerProfile.BusinessLicenseUrl = draft.BusinessLicenseUrl;
            if (!string.IsNullOrWhiteSpace(draft.AvatarUrl))
            {
                sellerProfile.AvatarUrl = draft.AvatarUrl;
            }
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
                ShopName = draft.ShopName,
                BusinessEmail = draft.BusinessEmail,
                Hotline = draft.Hotline,
                PickupAddress = draft.PickupAddress,
                ProvinceId = draft.ProvinceId,
                DistrictId = draft.DistrictId,
                WardCode = draft.WardCode,
                IdCardFrontUrl = draft.IdCardFrontUrl,
                IdCardBackUrl = draft.IdCardBackUrl,
                BusinessLicenseUrl = draft.BusinessLicenseUrl,
                AvatarUrl = draft.AvatarUrl,
                Status = SellerProfileStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _dbContext.SellerProfiles.Add(sellerProfile);
        }

        // 7. Reset attempts and Mark OTP consumed
        await _otpService.ResetOtpAttemptsAsync(normalizedEmail, purpose: "seller_registration");
        await _otpService.MarkOtpConsumedAsync(normalizedEmail, purpose: "seller_registration");

        // 8. Add Audit Log
        _dbContext.AddAuditLog(
            action: AuditAction.UserUpdate,
            entityName: nameof(SellerProfile),
            entityId: sellerProfile.Id.ToString(),
            newValues: new { status = "Pending", shopName = sellerProfile.ShopName, businessEmail = sellerProfile.BusinessEmail },
            userId: userId.ToString());

        // 9. Local DB commit
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[SELLER OTP VERIFY] Verified and committed SellerProfile '{SellerId}' for User '{UserId}'. Status: PENDING.",
            sellerProfile.Id, userId);

        return new VerifySellerOtpResponseDto(
            SellerId: sellerProfile.Id.ToString(),
            UserId: userId.ToString(),
            Status: "Pending",
            BusinessEmail: sellerProfile.BusinessEmail,
            ShopName: sellerProfile.ShopName,
            VerifiedAt: DateTimeOffset.UtcNow,
            Message: "Business email verified successfully. Your seller profile is pending administrator approval."
        );
    }

    /// <summary>
    /// Gửi lại mã OTP xác thực email doanh nghiệp cho đơn đăng ký đang chờ nộp.
    /// Kiểm tra thời gian chờ Cooldown (60 giây) trước khi sinh mã OTP mới.
    /// </summary>
    /// <param name="userId">ID người dùng yêu cầu.</param>
    /// <param name="businessEmail">Email doanh nghiệp nhận OTP.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu.</param>
    public async Task ResendSellerOtpAsync(
        Guid userId,
        string businessEmail,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "User identifier is required.");
        }

        // Verify draft exists in Redis
        var draftJson = await _otpService.GetDraftAsync($"user:seller_draft:{userId}");
        if (string.IsNullOrWhiteSpace(draftJson))
        {
            _logger.LogWarning("[SELLER OTP RESEND] Draft not found in Redis for User '{UserId}'.", userId);
            throw new DomainException("DRAFT_NOT_FOUND", "Registration session expired or does not exist. Please submit registration again.");
        }

        var draft = JsonSerializer.Deserialize<SellerRegistrationDraft>(draftJson);
        var targetEmail = !string.IsNullOrWhiteSpace(draft?.BusinessEmail)
            ? draft.BusinessEmail
            : businessEmail.Trim().ToLowerInvariant();

        if (await _otpService.IsCooldownActiveAsync(targetEmail, purpose: "seller_registration"))
        {
            _logger.LogWarning("[SELLER OTP RESEND] Rejected: Cooldown is active for '{Email}'.", targetEmail);
            throw new DomainException("OTP_COOLDOWN_ACTIVE", "Please wait 60 seconds before requesting a new OTP.", 429);
        }

        // Invalidate old OTP and generate new 6-digit OTP
        var newOtpCode = Random.Shared.Next(100000, 999999).ToString();
        await _otpService.InvalidateExistingOtpsAsync(targetEmail, purpose: "seller_registration");
        await _otpService.SaveOtpAsync(
            targetEmail,
            newOtpCode,
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(15),
            purpose: "seller_registration"
        );
        await _otpService.SetCooldownAsync(targetEmail, TimeSpan.FromSeconds(60), purpose: "seller_registration");

        _logger.LogInformation("[SELLER OTP RESEND] Resent OTP for '{Email}' | CODE: >>> {OtpCode} <<< | Valid: 5 mins", targetEmail, newOtpCode);

        if (_emailNotificationClient != null)
        {
            try
            {
                var user = await _dbContext.Users.FindAsync(new object[] { userId }, cancellationToken);
                var fullName = user != null ? $"{user.FirstName} {user.LastName}".Trim() : "Seller";
                await _emailNotificationClient.SendOtpEmailAsync(
                    targetEmail,
                    fullName,
                    newOtpCode,
                    5,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to dispatch resent OTP email for seller registration. OTP: {Otp}", newOtpCode);
            }
        }
    }

    /// <summary>
    /// Tra cứu tiến trình và trạng thái xét duyệt đơn đăng ký Người bán của người dùng hiện tại.
    /// Trả về trạng thái: NOT_REGISTERED, PENDING, APPROVED, REJECTED hoặc SUSPENDED.
    /// </summary>
    /// <param name="userId">ID người dùng đang đăng nhập.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu.</param>
    /// <returns>Trạng thái hồ sơ kèm hướng dẫn bước tiếp theo (NextStep).</returns>
    public async Task<SellerRegistrationStatusResponseDto> GetSellerRegistrationStatusAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "User identifier is required.");
        }

        // 1. Check if draft exists in Redis
        var draftJson = await _otpService.GetDraftAsync($"user:seller_draft:{userId}");
        if (!string.IsNullOrWhiteSpace(draftJson))
        {
            var draft = JsonSerializer.Deserialize<SellerRegistrationDraft>(draftJson);
            return new SellerRegistrationStatusResponseDto(
                Status: "OTP_VERIFICATION_REQUIRED",
                RejectionReason: null,
                NextStep: "VERIFY_OTP_VIA_POST_/api/v1/sellers/otp/verify",
                BusinessEmail: draft?.BusinessEmail
            );
        }

        // 2. Check if SellerProfile exists in DB
        var sellerProfile = await _dbContext.SellerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

        if (sellerProfile != null)
        {
            var statusStr = sellerProfile.Status.ToString();
            var nextStep = sellerProfile.Status switch
            {
                SellerProfileStatus.Pending => "AWAITING_STAFF_APPROVAL",
                SellerProfileStatus.Approved => "ONBOARDING_COMPLETED",
                SellerProfileStatus.Rejected => "RESUBMIT_REGISTRATION_ALLOWED",
                _ => null
            };

            return new SellerRegistrationStatusResponseDto(
                Status: statusStr,
                RejectionReason: sellerProfile.RejectionReason,
                NextStep: nextStep,
                BusinessEmail: sellerProfile.BusinessEmail
            );
        }

        return new SellerRegistrationStatusResponseDto(
            Status: "NOT_REGISTERED",
            RejectionReason: null,
            NextStep: "SUBMIT_REGISTRATION_VIA_POST_/api/v1/sellers/registration",
            BusinessEmail: null
        );
    }

    // =========================================================================================
    // SECTION 2: THẨM ĐỊNH VÀ XÉT DUYỆT ĐƠN KYC NGƯỜI BÁN (SUPPORT STAFF KYC REVIEW)
    // Dành riêng cho nhân viên Support Staff theo nguyên tắc Phân tách trách nhiệm (SoD).
    // Bao gồm: Danh sách đơn chờ duyệt, chi tiết hồ sơ KYC (CCCD, GPKD), duyệt đơn, từ chối đơn.
    // =========================================================================================

    /// <summary>
    /// [Support Staff] Tra cứu danh sách các đơn đăng ký Người bán đang chờ duyệt có phân trang và bộ lọc.
    /// </summary>
    /// <param name="page">Số trang hiện tại (bắt đầu từ 1).</param>
    /// <param name="limit">Số bản ghi mỗi trang (mặc định 20).</param>
    /// <param name="status">Lọc theo trạng thái đơn: Pending, Approved, Rejected.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu.</param>
    /// <returns>Dữ liệu phân trang danh sách đơn đăng ký kèm metadata.</returns>
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
            Status: s.Status.ToString(),
            CreatedAt: s.CreatedAt
        )).ToList();

        return PagedData<AdminSellerRegistrationItemDto>.From(items, page, limit, total);
    }

    /// <summary>
    /// [Support Staff] Xem chi tiết hồ sơ đơn đăng ký Người bán để thẩm định KYC.
    /// Bao gồm ảnh chụp 2 mặt CCCD, Giấy phép kinh doanh, Địa chỉ kho lấy hàng và điểm uy tín.
    /// </summary>
    /// <param name="registrationId">ID đơn đăng ký hoặc ID người dùng.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu.</param>
    /// <returns>Chi tiết toàn bộ tài liệu hồ sơ KYC.</returns>
    public async Task<SellerRegistrationDetailsDto> GetSellerRegistrationDetailsAsync(
        Guid registrationId,
        CancellationToken cancellationToken = default)
    {
        if (registrationId == Guid.Empty)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Registration identifier is required.");
        }

        var profile = await _dbContext.SellerProfiles
            .AsNoTracking()
            .Include(s => s.User)
                .ThenInclude(u => u!.ReputationScore)
            .FirstOrDefaultAsync(s => s.Id == registrationId, cancellationToken);

        if (profile == null)
        {
            profile = await _dbContext.SellerProfiles
                .AsNoTracking()
                .Include(s => s.User)
                    .ThenInclude(u => u!.ReputationScore)
                .FirstOrDefaultAsync(s => s.UserId == registrationId, cancellationToken);
        }

        if (profile == null)
        {
            _logger.LogWarning("[SELLER REGISTRATION DETAILS] Seller registration '{RegistrationId}' not found.", registrationId);
            throw new DomainException("SELLER_REGISTRATION_NOT_FOUND", "Seller registration application not found.", 404);
        }

        var applicantName = profile.User != null
            ? $"{profile.User.FirstName} {profile.User.LastName}".Trim()
            : string.Empty;

        return new SellerRegistrationDetailsDto(
            RegistrationId: profile.Id.ToString(),
            UserId: profile.UserId.ToString(),
            ApplicantName: applicantName,
            ApplicantEmail: profile.User?.Email ?? profile.BusinessEmail ?? string.Empty,
            ApplicantPhone: profile.User?.PhoneNumber ?? profile.Hotline,
            ShopName: profile.ShopName,
            AvatarUrl: profile.AvatarUrl,
            BusinessEmail: profile.BusinessEmail,
            Hotline: profile.Hotline,
            IdentityCardNumber: profile.User?.IdentifyNumber ?? string.Empty,
            IdCardFrontUrl: profile.IdCardFrontUrl,
            IdCardBackUrl: profile.IdCardBackUrl,
            BusinessLicenseUrl: profile.BusinessLicenseUrl,
            PickupAddress: profile.PickupAddress,
            WarehouseAddress: profile.PickupAddress,
            ProvinceId: profile.ProvinceId,
            DistrictId: profile.DistrictId,
            WardCode: profile.WardCode,
            ReputationScore: profile.User?.ReputationScore?.CurrentScore ?? 50,
            Status: profile.Status.ToString(),
            RejectionReason: profile.RejectionReason,
            ReviewedBy: profile.ReviewedBy?.ToString(),
            ReviewedAt: profile.ReviewedAt,
            CreatedAt: profile.CreatedAt,
            UpdatedAt: profile.UpdatedAt
        );
    }

    /// <summary>
    /// [Support Staff] Phê duyệt đơn đăng ký Người bán.
    /// Áp dụng mô hình Downstream-First (tạo kho hàng tại FulfillmentService trước),
    /// sau đó chuyển đổi vai trò đơn nhất (gán Seller, xóa mềm Buyer) và hủy Refresh Tokens cũ.
    /// </summary>
    /// <param name="registrationId">ID đơn đăng ký được duyệt.</param>
    /// <param name="reviewerId">ID nhân viên Support Staff duyệt đơn.</param>
    /// <param name="dto">Dữ liệu phê duyệt bổ sung (tùy chọn).</param>
    /// <param name="cancellationToken">Token hủy yêu cầu.</param>
    /// <returns>Xác nhận duyệt đơn và chuyển đổi vai trò thành công.</returns>
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
                .ThenInclude(u => u!.Role)
            .Include(s => s.User)
                .ThenInclude(u => u!.ReputationScore)
            .FirstOrDefaultAsync(s => s.Id == registrationId, cancellationToken);

        if (sellerProfile == null)
        {
            sellerProfile = await _dbContext.SellerProfiles
                .Include(s => s.User)
                    .ThenInclude(u => u!.Role)
                .Include(s => s.User)
                    .ThenInclude(u => u!.ReputationScore)
                .FirstOrDefaultAsync(s => s.UserId == registrationId, cancellationToken);
        }

        if (sellerProfile == null)
        {
            _logger.LogWarning("[SELLER APPROVAL] Seller registration '{RegistrationId}' not found.", registrationId);
            throw new DomainException("REGISTRATION_NOT_FOUND", "Seller registration not found.");
        }

        // Validate status: ONLY Pending applications can be approved
        if (sellerProfile.Status == SellerProfileStatus.Approved)
        {
            _logger.LogWarning("[SELLER APPROVAL] Registration '{RegistrationId}' is already approved.", registrationId);
            throw new DomainException("ALREADY_APPROVED", "Seller registration has already been approved.");
        }

        if (sellerProfile.Status != SellerProfileStatus.Pending)
        {
            _logger.LogWarning("[SELLER APPROVAL] Registration '{RegistrationId}' is in status '{Status}' (not Pending).", registrationId, sellerProfile.Status);
            throw new DomainException("INVALID_STATUS", "Only pending seller registrations can be approved.");
        }

        var user = sellerProfile.User;
        if (user == null)
        {
            throw new DomainException("USER_NOT_FOUND", "Associated user account not found.");
        }

        if (user.Status != UserStatus.Active)
        {
            throw new DomainException("USER_NOT_ACTIVE", "User account must be active to approve seller registration.");
        }

        if (user.ReputationScore != null && user.ReputationScore.CurrentScore < 50)
        {
            throw new DomainException("REPUTATION_TOO_LOW", "User reputation score is below the required threshold (50).");
        }

        // PHA 2: DOWNSTREAM-FIRST — Call FulfillmentService to create warehouse BEFORE local DB commit
        var contactName = $"{user.FirstName} {user.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(contactName))
        {
            contactName = sellerProfile.ShopName;
        }

        _logger.LogInformation("[SELLER APPROVAL] Downstream-First: Calling FulfillmentService for Seller '{SellerId}', Shop '{ShopName}'...",
            sellerProfile.Id, sellerProfile.ShopName);

        var warehouseCreated = await _fulfillmentClient.CreateWarehouseAsync(
            sellerId: sellerProfile.Id,
            userId: sellerProfile.UserId,
            shopName: sellerProfile.ShopName,
            contactName: contactName,
            contactPhone: sellerProfile.Hotline,
            pickupAddress: sellerProfile.PickupAddress,
            provinceId: sellerProfile.ProvinceId,
            districtId: sellerProfile.DistrictId,
            wardCode: sellerProfile.WardCode,
            cancellationToken: cancellationToken);

        if (!warehouseCreated)
        {
            _logger.LogWarning("[SELLER APPROVAL] Fulfillment warehouse creation failed or timed out for Seller '{SellerId}'. Proceeding with seller approval (graceful degradation).", sellerProfile.Id);
        }

        // PHA 3: LOCAL COMMIT IN POSTGRESQL
        // 1. Assign Seller role to user (1 User 1 Role principle)
        var sellerRole = await _dbContext.Roles
            .FirstOrDefaultAsync(r => r.RoleCode == RoleType.Seller, cancellationToken);

        if (sellerRole != null)
        {
            var oldRoleId = user.RoleId;
            user.RoleId = sellerRole.Id;
            user.Role = sellerRole;
            _logger.LogInformation("[SELLER APPROVAL] Assigned role 'Seller' to UserId: '{UserId}'.", sellerProfile.UserId);

            _dbContext.AddAuditLog(
                action: AuditAction.RoleAssign,
                entityName: nameof(User),
                entityId: user.Id.ToString(),
                oldValues: new { roleId = oldRoleId },
                newValues: new { roleId = sellerRole.Id, role = "Seller" },
                userId: reviewerId?.ToString());
        }

        // Revoke all active refresh tokens for the user to force token rotation to 'seller' role
        var activeTokens = await _dbContext.RefreshTokens
            .Where(t => t.UserId == sellerProfile.UserId && !t.IsRevoked)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = DateTimeOffset.UtcNow;
            token.UpdatedAt = DateTimeOffset.UtcNow;
        }

        // 2. Update SellerProfile status and initialize isolated SellerScore (50 - Standard)
        sellerProfile.Status = SellerProfileStatus.Approved;
        sellerProfile.ReviewedBy = reviewerId;
        sellerProfile.ReviewedAt = DateTimeOffset.UtcNow;
        sellerProfile.UpdatedAt = DateTimeOffset.UtcNow;

        if (user.ReputationScore != null)
        {
            user.ReputationScore.InitializeSellerScore();
        }
        else
        {
            _dbContext.ReputationScores.Add(ReputationScore.CreateDefault(user.Id));
        }

        // 3. Record audit log
        _dbContext.AddAuditLog(
            action: AuditAction.SellerApprove,
            entityName: nameof(SellerProfile),
            entityId: sellerProfile.Id.ToString(),
            newValues: new { status = "Approved", note = dto?.Note },
            userId: reviewerId?.ToString());

        // 4. Local commit with compensating rollback if DB fails
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "[SELLER APPROVAL] DB commit failed after warehouse created for Seller '{SellerId}'. Initiating compensating deletion...", sellerProfile.Id);
            try
            {
                await _fulfillmentClient.DeleteWarehouseBySellerIdAsync(sellerProfile.Id, CancellationToken.None);
            }
            catch (Exception compEx)
            {
                _logger.LogError(compEx, "[SELLER APPROVAL] Failed to execute compensating warehouse deletion for Seller '{SellerId}'.", sellerProfile.Id);
            }
            throw;
        }

        _logger.LogInformation("[SELLER APPROVAL] Successfully approved seller '{SellerId}' by Reviewer '{ReviewerId}'.", sellerProfile.Id, reviewerId);

        // Dispatch approval email via Notification Service
        if (_emailNotificationClient != null)
        {
            var recipientEmail = !string.IsNullOrWhiteSpace(sellerProfile.BusinessEmail)
                ? sellerProfile.BusinessEmail.Trim()
                : user.Email?.Trim();

            if (!string.IsNullOrWhiteSpace(recipientEmail))
            {
                try
                {
                    await _emailNotificationClient.SendSellerApprovedEmailAsync(
                        recipientEmail,
                        contactName,
                        sellerProfile.ShopName,
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[SELLER APPROVAL] Failed to dispatch approval notification email to '{Email}'.", recipientEmail);
                }
            }
        }

        return new ApproveSellerRegistrationResponseDto(
            RegistrationId: sellerProfile.Id.ToString(),
            UserId: sellerProfile.UserId.ToString(),
            Status: "Approved",
            ApprovedAt: sellerProfile.ReviewedAt.Value
        );
    }

    /// <summary>
    /// [Support Staff] Từ chối đơn đăng ký Người bán do hồ sơ KYC không hợp lệ hoặc thiếu giấy tờ.
    /// </summary>
    /// <param name="registrationId">ID đơn đăng ký bị từ chối.</param>
    /// <param name="reviewerId">ID nhân viên Support Staff từ chối.</param>
    /// <param name="dto">Payload chứa lý do từ chối (RejectionReason).</param>
    /// <param name="cancellationToken">Token hủy yêu cầu.</param>
    /// <returns>Xác nhận từ chối đơn đăng ký.</returns>
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

        if (sellerProfile.Status == SellerProfileStatus.Approved)
        {
            throw new DomainException("INVALID_STATUS", "Approved seller registrations cannot be rejected.");
        }

        var reason = dto.RejectionReason.Trim();
        sellerProfile.Status = SellerProfileStatus.Rejected;
        sellerProfile.RejectionReason = reason;
        sellerProfile.ReviewedBy = reviewerId;
        sellerProfile.ReviewedAt = DateTimeOffset.UtcNow;
        sellerProfile.UpdatedAt = DateTimeOffset.UtcNow;

        // Compensating cleanup in case downstream warehouse had been created
        try
        {
            await _fulfillmentClient.DeleteWarehouseBySellerIdAsync(sellerProfile.Id, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SELLER REJECT] Compensating warehouse deletion skipped or failed for Seller '{SellerId}'.", sellerProfile.Id);
        }

        _dbContext.AddAuditLog(
            action: AuditAction.SellerReject,
            entityName: nameof(SellerProfile),
            entityId: sellerProfile.Id.ToString(),
            newValues: new { status = "Rejected", reason },
            userId: reviewerId?.ToString());

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[SELLER REJECTION] Rejected seller registration '{SellerId}' with reason: '{Reason}'.", sellerProfile.Id, reason);

        // Dispatch rejection email via Notification Service
        if (_emailNotificationClient != null)
        {
            var user = await _dbContext.Users.FindAsync(new object[] { sellerProfile.UserId }, cancellationToken);
            var recipientEmail = !string.IsNullOrWhiteSpace(sellerProfile.BusinessEmail)
                ? sellerProfile.BusinessEmail.Trim()
                : user?.Email?.Trim();

            if (!string.IsNullOrWhiteSpace(recipientEmail))
            {
                var contactName = user != null
                    ? $"{user.FirstName} {user.LastName}".Trim()
                    : sellerProfile.ShopName;

                if (string.IsNullOrWhiteSpace(contactName))
                {
                    contactName = sellerProfile.ShopName;
                }

                try
                {
                    await _emailNotificationClient.SendSellerRejectedEmailAsync(
                        recipientEmail,
                        contactName,
                        sellerProfile.ShopName,
                        reason,
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[SELLER REJECTION] Failed to dispatch rejection notification email to '{Email}'.", recipientEmail);
                }
            }
        }

        return new RejectSellerRegistrationResponseDto(
            RegistrationId: sellerProfile.Id.ToString(),
            UserId: sellerProfile.UserId.ToString(),
            Status: "Rejected",
            RejectedAt: sellerProfile.ReviewedAt.Value,
            RejectionReason: reason
        );
    }

    // =========================================================================================
    // SECTION 3: VẬN HÀNH GIAN HÀNG, HỒ SƠ SHOP VÀ KỶ LUẬT (SHOP OPERATIONS & SANCTIONS)
    // Bao gồm: Xem/cập nhật hồ sơ shop của mình, hồ sơ công khai, hồ sơ admin, đình chỉ và kích hoạt lại shop.
    // =========================================================================================

    /// <summary>
    /// [Seller] Tra cứu thông tin hồ sơ gian hàng của chính mình đang đăng nhập.
    /// </summary>
    /// <param name="userId">ID người bán đang đăng nhập.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu.</param>
    /// <returns>Hồ sơ gian hàng của người bán.</returns>
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
        var sellerReputationScore = sellerProfile.User?.ReputationScore?.SellerScore ?? 50;
        var sellerTrustLevel = sellerProfile.User?.ReputationScore?.SellerTrustLevel.ToString() ?? TrustLevel.Standard.ToString();

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
            Status: sellerProfile.Status.ToString(),
            RatingAvg: ratingAvg,
            ReputationScore: reputationScore,
            TrustLevel: trustLevel,
            CreatedAt: sellerProfile.CreatedAt,
            UpdatedAt: sellerProfile.UpdatedAt,
            SellerReputationScore: sellerReputationScore,
            SellerTrustLevel: sellerTrustLevel
        );
    }

    /// <summary>
    /// [Seller] Cập nhật thông tin gian hàng và địa chỉ kho lấy hàng / đổi trả.
    /// </summary>
    /// <param name="userId">ID người bán đang đăng nhập.</param>
    /// <param name="dto">Dữ liệu cập nhật: Hotline, Avatar, Địa chỉ kho lấy hàng, Địa chỉ đổi trả.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu.</param>
    /// <returns>Hồ sơ gian hàng sau khi cập nhật thành công.</returns>
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

        var addressOrContactChanged = false;

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
                addressOrContactChanged = true;
            }
        }

        // 2. Update Hotline if provided
        if (!string.IsNullOrWhiteSpace(dto.Hotline))
        {
            sellerProfile.Hotline = dto.Hotline.Trim();
            addressOrContactChanged = true;
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

        // 5. Update warehouse pickup address fields
        var newAddress = !string.IsNullOrWhiteSpace(dto.PickupAddress)
            ? dto.PickupAddress.Trim()
            : !string.IsNullOrWhiteSpace(dto.StreetAddress)
                ? dto.StreetAddress.Trim()
                : null;

        if (!string.IsNullOrWhiteSpace(newAddress) && newAddress != sellerProfile.PickupAddress)
        {
            sellerProfile.PickupAddress = newAddress;
            addressOrContactChanged = true;
        }

        if (dto.ProvinceId.HasValue && dto.ProvinceId.Value > 0 && dto.ProvinceId.Value != sellerProfile.ProvinceId)
        {
            sellerProfile.ProvinceId = dto.ProvinceId.Value;
            addressOrContactChanged = true;
        }

        if (dto.DistrictId.HasValue && dto.DistrictId.Value > 0 && dto.DistrictId.Value != sellerProfile.DistrictId)
        {
            sellerProfile.DistrictId = dto.DistrictId.Value;
            addressOrContactChanged = true;
        }

        if (!string.IsNullOrWhiteSpace(dto.WardCode) && dto.WardCode.Trim() != sellerProfile.WardCode)
        {
            sellerProfile.WardCode = dto.WardCode.Trim();
            addressOrContactChanged = true;
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

        // 7. Sync updated warehouse details to FulfillmentService (best effort)
        if (addressOrContactChanged && sellerProfile.Status == SellerProfileStatus.Approved)
        {
            try
            {
                _ = _fulfillmentClient.UpdateWarehouseAsync(
                    sellerId: sellerProfile.Id,
                    shopName: sellerProfile.ShopName,
                    contactPhone: sellerProfile.Hotline,
                    pickupAddress: sellerProfile.PickupAddress,
                    provinceId: sellerProfile.ProvinceId,
                    districtId: sellerProfile.DistrictId,
                    wardCode: sellerProfile.WardCode,
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[SELLER PROFILE UPDATE] Background warehouse sync failed for Seller '{SellerId}'.", sellerProfile.Id);
            }
        }

        _logger.LogInformation("[SELLER PROFILE UPDATE] Successfully updated seller profile '{SellerId}' for User '{UserId}'.", sellerProfile.Id, userId);

        return new SellerOwnProfileUpdatedResponseDto(
            ShopId: sellerProfile.Id.ToString(),
            SellerId: sellerProfile.Id.ToString(),
            ShopName: sellerProfile.ShopName,
            UpdatedAt: sellerProfile.UpdatedAt,
            Message: "Seller profile updated successfully."
        );
    }

    /// <summary>
    /// [Public] Tra cứu thông tin công khai của một gian hàng cho khách mua hàng xem shop.
    /// Ẩn toàn bộ thông tin nhạy cảm (CCCD, email cá nhân).
    /// </summary>
    /// <param name="id">ID người bán hoặc ID gian hàng.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu.</param>
    /// <returns>Thông tin công khai của shop: Tên shop, Avatar, Hotline, Tỉnh/thành, Trạng thái.</returns>
    public async Task<PublicSellerProfileResponseDto> GetPublicSellerProfileAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Seller or shop identifier is required.");
        }

        var sellerProfile = await _dbContext.SellerProfiles
            .AsNoTracking()
            .Include(sp => sp.User)
                .ThenInclude(u => u!.ReviewsReceived)
            .FirstOrDefaultAsync(sp => (sp.Id == id || sp.UserId == id) && sp.Status == SellerProfileStatus.Approved, cancellationToken);

        if (sellerProfile == null || sellerProfile.User == null || sellerProfile.User.IsDeleted || sellerProfile.User.Status != Domain.Enums.UserStatus.Active)
        {
            _logger.LogWarning("[PUBLIC SELLER PROFILE] Approved seller profile was not found or inactive for identifier: '{Id}'.", id);
            throw new DomainException("SELLER_NOT_FOUND", "Seller shop was not found.", 404);
        }

        var reviews = sellerProfile.User?.ReviewsReceived?.Where(r => !r.IsDisabled).ToList();
        double ratingAvg = 5.0;
        if (reviews != null && reviews.Count > 0)
        {
            ratingAvg = Math.Round(reviews.Average(r => r.Rating), 2);
        }

        return new PublicSellerProfileResponseDto(
            ShopId: sellerProfile.Id.ToString(),
            SellerId: sellerProfile.UserId.ToString(),
            ShopName: sellerProfile.ShopName,
            LogoUrl: sellerProfile.AvatarUrl ?? sellerProfile.User?.AvatarUrl,
            Description: null,
            Hotline: sellerProfile.Hotline,
            RatingAvg: ratingAvg,
            TotalProducts: 0,
            CreatedAt: sellerProfile.CreatedAt
        );
    }

    /// <summary>
    /// [Admin / Staff] Tra cứu hồ sơ người bán toàn diện phục vụ kiểm toán, đối soát và khiếu nại.
    /// </summary>
    /// <param name="sellerId">ID hồ sơ người bán.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu.</param>
    /// <returns>Hồ sơ người bán chi tiết kèm lịch sử đánh giá và kho hàng.</returns>
    public async Task<AdminSellerProfileResponseDto> GetAdminSellerProfileAsync(
        Guid sellerId,
        CancellationToken cancellationToken = default)
    {
        if (sellerId == Guid.Empty)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Seller identifier is required.");
        }

        var profile = await _dbContext.SellerProfiles
            .AsNoTracking()
            .Include(s => s.User)
                .ThenInclude(u => u!.ReputationScore)
            .Include(s => s.User)
                .ThenInclude(u => u!.ReviewsReceived)
            .Include(s => s.ReviewedByUser)
            .FirstOrDefaultAsync(s => s.Id == sellerId || s.UserId == sellerId, cancellationToken);

        if (profile == null)
        {
            _logger.LogWarning("[ADMIN SELLER PROFILE] Seller profile not found for identifier: '{SellerId}'.", sellerId);
            throw new DomainException("SELLER_NOT_FOUND", "Seller profile was not found.", 404);
        }

        var reviews = profile.User?.ReviewsReceived?.Where(r => !r.IsDisabled).ToList();
        double ratingAvg = 5.0;
        int totalPositiveReviews = 0;
        int totalNegativeReviews = 0;
        if (reviews != null && reviews.Count > 0)
        {
            ratingAvg = Math.Round(reviews.Average(r => r.Rating), 2);
            totalPositiveReviews = reviews.Count(r => r.Rating >= 4.0);
            totalNegativeReviews = reviews.Count(r => r.Rating <= 2.0);
        }

        var reputationScore = profile.User?.ReputationScore?.CurrentScore ?? 50;
        var trustLevel = profile.User?.ReputationScore?.TrustLevel.ToString() ?? TrustLevel.Standard.ToString();
        var sellerReputationScore = profile.User?.ReputationScore?.SellerScore ?? 50;
        var sellerTrustLevel = profile.User?.ReputationScore?.SellerTrustLevel.ToString() ?? TrustLevel.Standard.ToString();
        var penaltyCount = (profile.User?.ReputationScore?.SellerPenaltyCount ?? 0) > 0
            ? profile.User!.ReputationScore!.SellerPenaltyCount
            : (profile.User?.ReputationScore?.PenaltyCount ?? 0);

        var ownerFullName = profile.User != null
            ? $"{profile.User.FirstName} {profile.User.LastName}".Trim()
            : string.Empty;
        var ownerEmail = profile.User?.Email ?? profile.BusinessEmail ?? string.Empty;
        var ownerPhoneNumber = profile.User?.PhoneNumber;
        var ownerGender = profile.User?.Gender.ToString() ?? "Unspecified";
        var ownerDateOfBirth = profile.User?.DateOfBirth;
        var ownerAddress = profile.User?.Address;
        var identityCardNumber = profile.User?.IdentifyNumber;

        var reviewedByName = profile.ReviewedByUser != null
            ? $"{profile.ReviewedByUser.FirstName} {profile.ReviewedByUser.LastName}".Trim()
            : null;

        var bankInfo = new SellerBankInfoDto();
        var settlementInfo = new SellerSettlementInfoDto();

        return new AdminSellerProfileResponseDto(
            SellerId: profile.Id.ToString(),
            ShopId: profile.Id.ToString(),
            UserId: profile.UserId.ToString(),
            ShopName: profile.ShopName,
            AvatarUrl: profile.AvatarUrl,
            BusinessEmail: profile.BusinessEmail,
            Hotline: profile.Hotline,
            Description: null,
            PickupAddress: profile.PickupAddress,
            WarehouseAddress: profile.PickupAddress,
            ProvinceId: profile.ProvinceId,
            DistrictId: profile.DistrictId,
            WardCode: profile.WardCode,
            IdentityCardNumber: identityCardNumber,
            IdCardFrontUrl: profile.IdCardFrontUrl,
            IdCardBackUrl: profile.IdCardBackUrl,
            BusinessLicenseUrl: profile.BusinessLicenseUrl,
            OwnerFullName: ownerFullName,
            OwnerEmail: ownerEmail,
            OwnerPhoneNumber: ownerPhoneNumber,
            OwnerGender: ownerGender,
            OwnerDateOfBirth: ownerDateOfBirth,
            OwnerAddress: ownerAddress,
            BankInfo: bankInfo,
            SettlementInfo: settlementInfo,
            RatingAvg: ratingAvg,
            ReputationScore: reputationScore,
            TrustLevel: trustLevel,
            TotalPositiveReviews: totalPositiveReviews,
            TotalNegativeReviews: totalNegativeReviews,
            PenaltyCount: penaltyCount,
            Status: profile.Status.ToString(),
            RejectionReason: profile.RejectionReason,
            ReviewedBy: profile.ReviewedBy?.ToString(),
            ReviewedByName: reviewedByName,
            ReviewedAt: profile.ReviewedAt,
            CreatedAt: profile.CreatedAt,
            UpdatedAt: profile.UpdatedAt,
            SellerReputationScore: sellerReputationScore,
            SellerTrustLevel: sellerTrustLevel
        );
    }

    /// <summary>
    /// [Admin / Staff] Tạm đình chỉ quyền bán hàng của gian hàng (ShopStatus = Suspended).
    /// Cơ chế Target Role Sanction: Chỉ khóa quyền Bán hàng, tài khoản cá nhân vẫn được phép Mua hàng.
    /// </summary>
    /// <param name="sellerId">ID hồ sơ người bán cần đình chỉ.</param>
    /// <param name="dto">Lý do đình chỉ và số ngày hiệu lực.</param>
    /// <param name="adminId">ID người thực hiện (Admin hoặc Staff).</param>
    /// <param name="cancellationToken">Token hủy yêu cầu.</param>
    /// <returns>Xác nhận đình chỉ gian hàng.</returns>
    public async Task<SuspendSellerResponseDto> SuspendSellerAsync(
        Guid sellerId,
        SuspendSellerDto dto,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.Reason))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Reason for suspending seller is required.");
        }

        var trimmedReason = dto.Reason.Trim();
        if (trimmedReason.Length > 1000)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Suspension reason cannot exceed 1000 characters.");
        }

        var profile = await _dbContext.SellerProfiles
            .Include(sp => sp.User)
            .FirstOrDefaultAsync(sp => sp.Id == sellerId || sp.UserId == sellerId, cancellationToken);

        if (profile == null)
        {
            throw new DomainException("SELLER_NOT_FOUND", "Seller profile was not found.", 404);
        }

        if (profile.Status == SellerProfileStatus.Suspended)
        {
            throw new DomainException("SELLER_ALREADY_SUSPENDED", "Seller profile is already suspended.", 400);
        }

        var now = DateTimeOffset.UtcNow;
        DateTimeOffset? expiresAt = dto.DurationDays.HasValue && dto.DurationDays.Value > 0
            ? now.AddDays(dto.DurationDays.Value)
            : null;

        var oldStatus = profile.Status.ToString();
        profile.Suspend(trimmedReason, adminId);

        var penalty = new UserPenalty
        {
            Id = Guid.NewGuid(),
            UserId = profile.UserId,
            TargetRole = TargetRole.Seller,
            ViolationType = PenaltyViolationType.Other,
            Reason = trimmedReason,
            PenaltyApplied = PenaltyType.Restriction,
            PointsDeducted = 0,
            Status = PenaltyStatus.Active,
            IssuedAt = now,
            ExpiresAt = expiresAt,
            CreatedBy = adminId,
            PolicyClauseCode = dto.PolicyClauseCode ?? "SELLER_OPERATIONAL_VIOLATION",
            EvidenceUrls = dto.EvidenceUrls ?? new System.Collections.Generic.List<string>(),
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false
        };
        _dbContext.UserPenalties.Add(penalty);

        _dbContext.AddAuditLog(
            action: AuditAction.SellerReject,
            entityName: "SellerProfile",
            entityId: profile.Id.ToString(),
            oldValues: new System.Collections.Generic.Dictionary<string, object?> { ["Status"] = oldStatus },
            newValues: new System.Collections.Generic.Dictionary<string, object?> { ["Status"] = SellerProfileStatus.Suspended.ToString(), ["Reason"] = trimmedReason, ["ExpiresAt"] = expiresAt },
            userId: adminId?.ToString(),
            changedColumns: new[] { "Status", "RejectionReason", "ReviewedBy", "ReviewedAt" });

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[SELLER SUSPEND] Suspended seller '{SellerId}' (UserId: '{UserId}') by admin '{AdminId}'. Reason: {Reason}",
            profile.Id, profile.UserId, adminId, trimmedReason);

        return new SuspendSellerResponseDto(
            SellerId: profile.Id.ToString(),
            ShopName: profile.ShopName,
            Status: profile.Status.ToString(),
            SuspendedAt: now,
            ExpiresAt: expiresAt,
            Reason: trimmedReason
        );
    }

    /// <summary>
    /// [Admin / Staff] Kích hoạt lại quyền bán hàng cho gian hàng đã khắc phục vi phạm (ShopStatus = Active).
    /// </summary>
    /// <param name="sellerId">ID hồ sơ người bán cần kích hoạt lại.</param>
    /// <param name="dto">Lý do mở lại gian hàng (tùy chọn).</param>
    /// <param name="adminId">ID người thực hiện (Admin hoặc Staff).</param>
    /// <param name="cancellationToken">Token hủy yêu cầu.</param>
    /// <returns>Xác nhận kích hoạt lại gian hàng.</returns>
    public async Task<ReactivateSellerResponseDto> ReactivateSellerAsync(
        Guid sellerId,
        ReactivateSellerDto? dto = null,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        var profile = await _dbContext.SellerProfiles
            .Include(sp => sp.User)
            .FirstOrDefaultAsync(sp => sp.Id == sellerId || sp.UserId == sellerId, cancellationToken);

        if (profile == null)
        {
            throw new DomainException("SELLER_NOT_FOUND", "Seller profile was not found.", 404);
        }

        if (profile.Status != SellerProfileStatus.Suspended)
        {
            throw new DomainException("SELLER_NOT_SUSPENDED", "Seller profile is not currently suspended.", 400);
        }

        if (profile.User != null && profile.User.Status == UserStatus.Banned)
        {
            throw new DomainException("CANNOT_REACTIVATE_BANNED_USER_SHOP", "Cannot reactivate seller shop while the owner user account is banned.", 400);
        }

        var reason = !string.IsNullOrWhiteSpace(dto?.Reason) ? dto.Reason.Trim() : "Seller reactivated by administrator.";
        var now = DateTimeOffset.UtcNow;
        var oldStatus = profile.Status.ToString();

        profile.Reactivate();

        var activeSellerPenalties = await _dbContext.UserPenalties
            .Where(p => p.UserId == profile.UserId && p.TargetRole == TargetRole.Seller && p.Status == PenaltyStatus.Active)
            .ToListAsync(cancellationToken);

        foreach (var p in activeSellerPenalties)
        {
            p.Revoke(adminId ?? Guid.Empty, reason);
        }

        _dbContext.AddAuditLog(
            action: AuditAction.SellerApprove,
            entityName: "SellerProfile",
            entityId: profile.Id.ToString(),
            oldValues: new System.Collections.Generic.Dictionary<string, object?> { ["Status"] = oldStatus },
            newValues: new System.Collections.Generic.Dictionary<string, object?> { ["Status"] = SellerProfileStatus.Approved.ToString(), ["Reason"] = reason },
            userId: adminId?.ToString(),
            changedColumns: new[] { "Status" });

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[SELLER REACTIVATE] Reactivated seller '{SellerId}' (UserId: '{UserId}') by admin '{AdminId}'.",
            profile.Id, profile.UserId, adminId);

        return new ReactivateSellerResponseDto(
            SellerId: profile.Id.ToString(),
            ShopName: profile.ShopName,
            Status: profile.Status.ToString(),
            ReactivatedAt: now,
            Reason: reason
        );
    }
}
