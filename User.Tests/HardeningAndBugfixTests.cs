namespace User.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using User.Api.Controllers;
using User.Application.Common.Extensions;
using User.Application.Interfaces;
using User.Application.Models;
using User.Application.Services.Admin;
using User.Application.Services.AdminConfig;
using User.Application.Services.Auth;
using User.Application.Services.Internal;
using User.Application.Services.Profile;
using User.Application.Services.Report;
using User.Application.Services.Review;
using User.Application.Services.Seller;
using User.Domain.Entities;
using User.Domain.Enums;
using User.Domain.Exceptions;
using User.Infrastructure.Persistence;
using User.Infrastructure.Services.Redis;
using Xunit;

public sealed class HardeningAndBugfixTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"hardening_test_db_{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    private sealed class StubFulfillmentClient : IFulfillmentServiceClient
    {
        public int UpdateCalls { get; private set; }
        public Guid LastUpdatedSellerId { get; private set; }
        public CancellationToken LastCancellationToken { get; private set; }

        public Task<bool> CreateWarehouseAsync(Guid sellerId, Guid userId, string shopName, string contactName, string contactPhone, string pickupAddress, int provinceId, int districtId, string wardCode, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<bool> UpdateWarehouseAsync(Guid sellerId, string shopName, string contactPhone, string pickupAddress, int provinceId, int districtId, string wardCode, CancellationToken cancellationToken = default)
        {
            UpdateCalls++;
            LastUpdatedSellerId = sellerId;
            LastCancellationToken = cancellationToken;
            return Task.FromResult(true);
        }

        public Task<bool> DeleteWarehouseBySellerIdAsync(Guid sellerId, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }

    private sealed class StubFileStorageService : IFileStorageService
    {
        public Task<StoredFileResult> UploadAsync(Stream content, string fileName, string contentType, string folder, bool isPrivate = false, bool isVideo = false, CancellationToken cancellationToken = default)
            => Task.FromResult(new StoredFileResult($"https://cdn.nexus.local/{folder}/{fileName}", $"{folder}/{fileName}", content.Length, contentType));

        public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public string GenerateSignedUrl(string key, bool isPrivate = false, int expiresInMinutes = 60)
            => $"https://cdn.nexus.local/signed/{key}";
    }

    private sealed class StubCommerceOrderClient : ICommerceOrderClient
    {
        private readonly OrderReviewReference? _reference;
        public StubCommerceOrderClient(OrderReviewReference? reference) => _reference = reference;

        public Task<OrderReviewReference?> GetReviewReferenceAsync(string referenceType, Guid referenceId, CancellationToken cancellationToken = default)
            => Task.FromResult(_reference);
    }

    private sealed class StubWalletClient : IWalletServiceClient
    {
        public List<(Guid UserId, string Reason)> Frozen { get; } = new();
        public List<(Guid UserId, string Reason)> Unfrozen { get; } = new();

        public Task<bool> FreezeWalletAsync(Guid userId, string reason, CancellationToken cancellationToken = default)
        {
            Frozen.Add((userId, reason));
            return Task.FromResult(true);
        }

        public Task<bool> UnfreezeWalletAsync(Guid userId, string reason, CancellationToken cancellationToken = default)
        {
            Unfrozen.Add((userId, reason));
            return Task.FromResult(true);
        }
    }

    private sealed class StubJwtGenerator : IJwtTokenGenerator
    {
        public string GenerateToken(User user, IEnumerable<string> roles, IEnumerable<string>? permissions, out DateTime expiresAt)
            => GenerateToken(user, roles, permissions, null, out expiresAt);

        public string GenerateToken(User user, IEnumerable<string> roles, IEnumerable<string>? permissions, Guid? sessionId, out DateTime expiresAt)
        {
            expiresAt = DateTime.UtcNow.AddMinutes(15);
            return "jwt-token";
        }

        public string GenerateToken(User user, IEnumerable<string> roles, out DateTime expiresAt)
            => GenerateToken(user, roles, null, null, out expiresAt);
    }

    [Fact]
    public void EnumExtensions_RejectsCommaSeparatedStrings_ForNonFlagsEnums()
    {
        Assert.False("Active,Banned".TryToEnum<UserStatus>(out _));
        Assert.False("0,Active".TryToEnum<UserStatus>(out _));
        Assert.Throws<ArgumentException>(() => "Active,Banned".ToEnum<UserStatus>());
    }

    [Fact]
    public void RoleExtensions_GetInheritedRoleClaims_StopsOnParentCycle()
    {
        var roleA = new Role { Id = Guid.NewGuid(), RoleCode = RoleType.Seller, RoleName = "Seller" };
        var roleB = new Role { Id = Guid.NewGuid(), RoleCode = RoleType.Buyer, RoleName = "Buyer", ParentRole = roleA };
        roleA.ParentRole = roleB;

        var claims = roleB.GetInheritedRoleClaims();

        Assert.Contains("buyer", claims);
        Assert.Contains("seller", claims);
        Assert.Equal(2, claims.Count);
    }

    [Fact]
    public async Task ProfileService_UpdateProfileAsync_RejectsAvatarUrlOver500Chars()
    {
        using var db = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        db.Users.Add(new User { Id = userId, Email = "p@nexus.local", FirstName = "A", LastName = "B", Status = UserStatus.Active });
        await db.SaveChangesAsync();

        var service = new ProfileService(db, NullLogger<ProfileService>.Instance);
        var ex = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            service.UpdateProfileAsync(userId, new UpdateProfileDto(AvatarUrl: new string('a', 501))));

        Assert.Equal("VAL_INVALID_INPUT", ex.ErrorCode);
    }

    [Fact]
    public async Task AuthController_Logout_DoesNotFallbackToAuthorizationBearerHeader()
    {
        using var db = CreateInMemoryDbContext();
        var otp = new RedisOtpService(NullLogger<RedisOtpService>.Instance);
        var authService = new AuthService(db, otp, NullLogger<AuthService>.Instance, new StubJwtGenerator());
        var controller = new AuthController(authService, NullLogger<AuthController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        controller.Request.Headers.Authorization = "Bearer some.jwt.token";

        var ex = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            controller.Logout(new LogoutApiRequest(null), CancellationToken.None));

        Assert.Equal("VAL_INVALID_INPUT", ex.ErrorCode);
    }

    [Fact]
    public async Task AuthService_RegisterAsync_Returns409_ForSoftDeletedEmailAndUsername()
    {
        using var db = CreateInMemoryDbContext();
        var otp = new RedisOtpService(NullLogger<RedisOtpService>.Instance);
        var authService = new AuthService(db, otp, NullLogger<AuthService>.Instance, new StubJwtGenerator());

        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "deleted@nexus.local",
            Username = "deleteduser",
            FirstName = "Del",
            LastName = "User",
            Status = UserStatus.Deleted,
            IsDeleted = true
        });
        await db.SaveChangesAsync();

        var emailEx = await Assert.ThrowsAsync<DomainException>(() =>
            authService.RegisterAsync(new RegisterDto("deleted@nexus.local", "Password123!", "New", "User")));
        Assert.Equal("EMAIL_ALREADY_EXISTS", emailEx.ErrorCode);
        Assert.Equal(409, emailEx.StatusCode);

        var usernameEx = await Assert.ThrowsAsync<DomainException>(() =>
            authService.RegisterAsync(new RegisterDto("fresh@nexus.local", "Password123!", "New", "User", Username: "deleteduser")));
        Assert.Equal("USERNAME_ALREADY_EXISTS", usernameEx.ErrorCode);
        Assert.Equal(409, usernameEx.StatusCode);
    }

    [Fact]
    public async Task AuthService_LoginAsync_SharesLockoutCounterBetweenEmailAndUsername()
    {
        using var db = CreateInMemoryDbContext();
        var otp = new RedisOtpService(NullLogger<RedisOtpService>.Instance);
        var authService = new AuthService(db, otp, NullLogger<AuthService>.Instance, new StubJwtGenerator());

        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "locktest@nexus.local",
            Username = "locktester",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("CorrectPass123!"),
            FirstName = "Lock",
            LastName = "Test",
            Status = UserStatus.Active
        });
        await db.SaveChangesAsync();

        for (int i = 0; i < 3; i++)
        {
            await Assert.ThrowsAsync<DomainException>(() =>
                authService.LoginAsync(new LoginDto("locktest@nexus.local", "WrongPass!")));
        }

        await Assert.ThrowsAsync<DomainException>(() =>
            authService.LoginAsync(new LoginDto("locktester", "WrongPass!")));

        var lockedEx = await Assert.ThrowsAsync<DomainException>(() =>
            authService.LoginAsync(new LoginDto("locktester", "WrongPass!")));
        Assert.Equal("ACCOUNT_LOCKED_TEMPORARY", lockedEx.ErrorCode);
    }

    [Fact]
    public async Task AdminUserService_GetUsersAsync_Throws400OnInvalidStatus()
    {
        using var db = CreateInMemoryDbContext();
        var service = new AdminUserService(db, NullLogger<AdminUserService>.Instance);

        var ex = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            service.GetUsersAsync(status: "not_a_real_status"));
        Assert.Equal("VAL_INVALID_INPUT", ex.ErrorCode);
    }

    [Fact]
    public async Task AdminUserService_UpdateUserAsync_EnforcesSelfBanAndLastAdminGuards()
    {
        using var db = CreateInMemoryDbContext();
        var adminRole = new Role { Id = Guid.NewGuid(), RoleCode = RoleType.Admin, RoleName = "Admin" };
        var adminId = Guid.NewGuid();
        db.Roles.Add(adminRole);
        db.Users.Add(new User
        {
            Id = adminId,
            Email = "admin@nexus.local",
            FirstName = "Admin",
            LastName = "One",
            Status = UserStatus.Active,
            RoleId = adminRole.Id,
            Role = adminRole
        });
        await db.SaveChangesAsync();

        var service = new AdminUserService(db, NullLogger<AdminUserService>.Instance);

        var selfBanEx = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            service.UpdateUserAsync(adminId, new UpdateAdminUserDto { Status = "Banned", Reason = "Self ban test" }, adminId));
        Assert.Equal("CANNOT_BAN_SELF", selfBanEx.ErrorCode);

        var lastAdminBanEx = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            service.UpdateUserAsync(adminId, new UpdateAdminUserDto { Status = "Banned", Reason = "Other admin ban" }, Guid.NewGuid()));
        Assert.Equal("CANNOT_BAN_LAST_ADMIN", lastAdminBanEx.ErrorCode);

        var lastAdminDelEx = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            service.UpdateUserAsync(adminId, new UpdateAdminUserDto { Status = "Deleted", Reason = "Delete last admin" }, Guid.NewGuid()));
        Assert.Equal("CANNOT_DELETE_LAST_ADMIN", lastAdminDelEx.ErrorCode);
    }

    [Fact]
    public async Task AdminPenaltyService_RevokePenaltyAsync_RejectsExpiredPenalty()
    {
        using var db = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var penaltyId = Guid.NewGuid();
        db.Users.Add(new User { Id = userId, Email = "u@nexus.local", FirstName = "U", LastName = "1", Status = UserStatus.Active });
        db.UserPenalties.Add(new UserPenalty
        {
            Id = penaltyId,
            UserId = userId,
            TargetRole = TargetRole.Seller,
            ViolationType = PenaltyViolationType.Other,
            PenaltyApplied = PenaltyType.Restriction,
            Reason = "Expired penalty",
            Status = PenaltyStatus.Active,
            IssuedAt = DateTimeOffset.UtcNow.AddDays(-10),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1)
        });
        await db.SaveChangesAsync();

        var service = new AdminPenaltyService(db, NullLogger<AdminPenaltyService>.Instance);
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.RevokePenaltyAsync(penaltyId, new RevokePenaltyDto { Reason = "Trying to revoke expired" }, Guid.NewGuid()));

        Assert.Equal("PENALTY_EXPIRED", ex.ErrorCode);
    }

    [Fact]
    public async Task AdminConfigService_UpdateConfigAsync_ValidatesDecimalRangeAndJson()
    {
        using var db = CreateInMemoryDbContext();
        db.SystemConfigs.AddRange(
            new SystemConfig
            {
                Id = Guid.NewGuid(),
                ConfigKey = "COMMISSION_RATE",
                ConfigValue = "0.05",
                DataType = ConfigDataType.Decimal,
                MinValue = 0.01m,
                MaxValue = 0.50m
            },
            new SystemConfig
            {
                Id = Guid.NewGuid(),
                ConfigKey = "FEATURE_FLAGS",
                ConfigValue = "{}",
                DataType = ConfigDataType.Json
            });
        await db.SaveChangesAsync();

        var service = new AdminConfigService(db, new MemoryCache(new MemoryCacheOptions()), NullLogger<AdminConfigService>.Instance);

        var decTypeEx = await Assert.ThrowsAsync<DomainException>(() =>
            service.UpdateConfigAsync(new UpdateSystemConfigRequest { Key = "COMMISSION_RATE", Value = "abc" }, Guid.NewGuid()));
        Assert.Equal("INVALID_CONFIG_VALUE", decTypeEx.ErrorCode);

        var decRangeEx = await Assert.ThrowsAsync<DomainException>(() =>
            service.UpdateConfigAsync(new UpdateSystemConfigRequest { Key = "COMMISSION_RATE", Value = "0.99" }, Guid.NewGuid()));
        Assert.Equal("INVALID_CONFIG_VALUE", decRangeEx.ErrorCode);

        var jsonEx = await Assert.ThrowsAsync<DomainException>(() =>
            service.UpdateConfigAsync(new UpdateSystemConfigRequest { Key = "FEATURE_FLAGS", Value = "{invalid-json" }, Guid.NewGuid()));
        Assert.Equal("INVALID_CONFIG_VALUE", jsonEx.ErrorCode);
    }

    [Fact]
    public async Task SellerService_GuardsPendingSuspend_EmailMismatch_KycLock_BannedShopKeyword_AndExpiredDuration()
    {
        using var db = CreateInMemoryDbContext();
        var otp = new RedisOtpService(NullLogger<RedisOtpService>.Instance);
        var fulfillment = new StubFulfillmentClient();
        var storage = new StubFileStorageService();
        var sellerService = new SellerService(db, otp, null, fulfillment, NullLogger<SellerService>.Instance, storage);

        var userId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        db.Users.Add(new User { Id = userId, Email = "seller1@nexus.local", FirstName = "S", LastName = "1", Status = UserStatus.Active });
        db.SellerProfiles.Add(new SellerProfile
        {
            Id = profileId,
            UserId = userId,
            ShopName = "Valid Shop",
            BusinessEmail = "shop@nexus.local",
            Hotline = "0900000001",
            PickupAddress = "123 Main St",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "W1",
            Status = SellerProfileStatus.Pending
        });
        await db.SaveChangesAsync();

        // 1. Cannot suspend Pending profile
        var suspendEx = await Assert.ThrowsAsync<DomainException>(() =>
            sellerService.SuspendSellerAsync(profileId, new SuspendSellerDto { Reason = "Not approved yet" }, Guid.NewGuid()));
        Assert.Equal("INVALID_STATUS", suspendEx.ErrorCode);

        // 2. Email mismatch on OTP verify
        await otp.SaveDraftAsync($"user:seller_draft:{userId}",
            System.Text.Json.JsonSerializer.Serialize(new SellerRegistrationDraft(userId, "Shop X", "expected@nexus.local", "090", "123", "Addr", 1, 1, "W1", null, null, null, null)),
            TimeSpan.FromMinutes(10));
        await otp.SaveOtpAsync("other@nexus.local", "123456", TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10), "seller_registration");

        var mismatchEx = await Assert.ThrowsAsync<DomainException>(() =>
            sellerService.VerifySellerRegistrationOtpAsync(userId, new VerifySellerOtpDto { BusinessEmail = "other@nexus.local", OtpCode = "123456" }));
        Assert.Equal("EMAIL_MISMATCH", mismatchEx.ErrorCode);

        // 3. Approved profile: KYC locked, banned keyword blocked, warehouse awaited with CancellationToken.None
        var profile = await db.SellerProfiles.FirstAsync(p => p.Id == profileId);
        profile.Status = SellerProfileStatus.Approved;
        await db.SaveChangesAsync();

        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00 };
        var kycEx = await Assert.ThrowsAsync<DomainException>(() =>
            sellerService.UploadSellerDocumentsAsync(
                userId,
                new SellerDocumentFileInput(new MemoryStream(pngBytes), "f.png", "image/png", pngBytes.Length),
                new SellerDocumentFileInput(new MemoryStream(pngBytes), "b.png", "image/png", pngBytes.Length),
                registrationId: profileId));
        Assert.Equal("KYC_LOCKED", kycEx.ErrorCode);

        var keywordEx = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            sellerService.UpdateSellerOwnProfileAsync(userId, new UpdateSellerOwnProfileDto { ShopName = "Nexus Official Store" }));
        Assert.Equal("VAL_BANNED_SHOP_NAME", keywordEx.ErrorCode);

        using var cts = new CancellationTokenSource();
        await sellerService.UpdateSellerOwnProfileAsync(userId, new UpdateSellerOwnProfileDto { Hotline = "0911111111" }, cts.Token);
        Assert.Equal(1, fulfillment.UpdateCalls);
        Assert.Equal(userId, fulfillment.LastUpdatedSellerId);
        Assert.Equal(CancellationToken.None, fulfillment.LastCancellationToken);

        // 4. Expired DurationDays suspension is lifted on GetSellerOwnProfileAsync
        profile.Status = SellerProfileStatus.Suspended;
        db.UserPenalties.Add(new UserPenalty
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TargetRole = TargetRole.Seller,
            ViolationType = PenaltyViolationType.Other,
            PenaltyApplied = PenaltyType.Restriction,
            Reason = "Temp 1 day",
            Status = PenaltyStatus.Active,
            IssuedAt = DateTimeOffset.UtcNow.AddDays(-2),
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1)
        });
        await db.SaveChangesAsync();

        var ownProfile = await sellerService.GetSellerOwnProfileAsync(userId);
        Assert.Equal("Approved", ownProfile.Status);
    }

    [Fact]
    public async Task ReportService_EnforcesCommerceFailClosed_AccountSellerTrack_AndNoUnfreezeOnBan()
    {
        using var db = CreateInMemoryDbContext();
        var reporterId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        db.Users.AddRange(
            new User { Id = reporterId, Email = "b@nexus.local", FirstName = "B", LastName = "1", Status = UserStatus.Active },
            new User { Id = sellerId, Email = "s@nexus.local", FirstName = "S", LastName = "1", Status = UserStatus.Active });
        db.SellerProfiles.Add(new SellerProfile
        {
            Id = Guid.NewGuid(),
            UserId = sellerId,
            ShopName = "Seller Shop",
            Hotline = "0900",
            PickupAddress = "Addr",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "W1",
            Status = SellerProfileStatus.Approved
        });
        db.ReputationScores.Add(new ReputationScore
        {
            Id = Guid.NewGuid(),
            UserId = sellerId,
            CurrentScore = 80,
            SellerScore = 60
        });
        await db.SaveChangesAsync();

        // 1. Fail closed when CommerceServiceClient is null
        var noCommerceService = new ReportService(db, NullLogger<ReportService>.Instance);
        var unavailEx = await Assert.ThrowsAsync<DomainException>(() =>
            noCommerceService.CreateReportAsync(reporterId, new CreateReportRequest
            {
                ReportedUserId = sellerId,
                TargetRole = TargetRole.Account,
                ReferenceType = ReferenceType.Order,
                ReferenceId = Guid.NewGuid().ToString(),
                ViolationType = ReportViolationType.Other,
                Description = "Order report without commerce client."
            }));
        Assert.Equal("ORDER_VERIFICATION_UNAVAILABLE", unavailEx.ErrorCode);

        // 2. TargetRole.Account on Order report deducts SellerScore and does NOT unfreeze wallet when banning
        var wallet = new StubWalletClient();
        var reportId = Guid.NewGuid();
        db.UserReports.Add(new UserReport
        {
            Id = reportId,
            ReporterId = reporterId,
            ReportedUserId = sellerId,
            TargetRole = TargetRole.Account,
            ReferenceType = ReferenceType.Order,
            ReferenceId = Guid.NewGuid().ToString("D"),
            ViolationType = ReportViolationType.Fraud,
            Description = "Fraudulent order report.",
            Status = ReportStatus.Pending
        });
        await db.SaveChangesAsync();

        var serviceWithWallet = new ReportService(db, NullLogger<ReportService>.Instance, walletServiceClient: wallet);
        await serviceWithWallet.ResolveReportAsync(reportId, new ResolveReportRequest
        {
            TargetRole = TargetRole.Account,
            ScoreDeduction = 10,
            BanAccount = true,
            FreezeWallet = false,
            ResolutionNote = "Confirmed fraud, banning account permanently."
        }, staffId);

        var score = await db.ReputationScores.FirstAsync(r => r.UserId == sellerId);
        Assert.Equal(80, score.CurrentScore);
        Assert.Equal(50, score.SellerScore);
        Assert.Empty(wallet.Unfrozen);
    }

    [Fact]
    public async Task ReviewService_CanonicalizesReferenceId_AndChecksSellerStatusInEligibility()
    {
        using var db = CreateInMemoryDbContext();
        var buyerId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        db.Users.AddRange(
            new User { Id = buyerId, Email = "b@nexus.local", FirstName = "B", LastName = "1", Status = UserStatus.Active },
            new User { Id = sellerId, Email = "s@nexus.local", FirstName = "S", LastName = "1", Status = UserStatus.Active });
        db.SellerProfiles.Add(new SellerProfile
        {
            Id = Guid.NewGuid(),
            UserId = sellerId,
            ShopName = "Pending Shop",
            Hotline = "0900",
            PickupAddress = "Addr",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "W1",
            Status = SellerProfileStatus.Pending
        });
        await db.SaveChangesAsync();

        var commerce = new StubCommerceOrderClient(new OrderReviewReference(
            ReferenceType: "Order",
            ReferenceId: orderId,
            BuyerId: buyerId,
            SellerId: sellerId,
            Status: "Completed",
            CompletedAt: DateTimeOffset.UtcNow.AddDays(-1)));

        var reviewService = new ReviewService(db, NullLogger<ReviewService>.Instance, commerceClient: commerce);

        // 1. Eligibility returns INVALID_REVIEW_ROLE when seller is not Approved
        var eligibility = await reviewService.GetEligibilityAsync(buyerId, ReferenceType.Order, orderId.ToString("N"));
        Assert.False(eligibility.CanReview);
        Assert.Equal("INVALID_REVIEW_ROLE", eligibility.Reason);

        // 2. Approve seller, create review with "N" format -> stored as "D" format, duplicate with "B" format rejected
        var sp = await db.SellerProfiles.FirstAsync(s => s.UserId == sellerId);
        sp.Status = SellerProfileStatus.Approved;
        await db.SaveChangesAsync();

        var created = await reviewService.CreateReviewAsync(buyerId, new CreateReviewRequest
        {
            RevieweeId = sellerId,
            ReferenceType = ReferenceType.Order,
            ReferenceId = orderId.ToString("N").ToUpperInvariant(),
            Rating = 5,
            Comment = "Great!"
        });
        Assert.Equal(orderId.ToString("D"), created.ReferenceId);

        var dupEx = await Assert.ThrowsAsync<DomainException>(() =>
            reviewService.CreateReviewAsync(buyerId, new CreateReviewRequest
            {
                RevieweeId = sellerId,
                ReferenceType = ReferenceType.Order,
                ReferenceId = orderId.ToString("B"),
                Rating = 5,
                Comment = "Duplicate!"
            }));
        Assert.Equal("REVIEW_ALREADY_EXISTS", dupEx.ErrorCode);
    }

    [Fact]
    public async Task InternalUserService_WritesCooldownZeroDeltaLog_ChecksAuctionRestriction_AndHandlesNullBatchRequest()
    {
        using var db = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        db.Users.Add(new User { Id = userId, Email = "a@nexus.local", FirstName = "A", LastName = "1", Status = UserStatus.Active });
        db.ReputationScores.Add(ReputationScore.CreateDefault(userId));
        db.ReputationLogs.Add(new ReputationLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TargetRole = TargetRole.Buyer,
            ChangeAmount = 1,
            ScoreAfter = 51,
            Reason = ReputationChangeReason.PositiveReview,
            ReferenceType = ReferenceType.Order,
            ReferenceId = "seed-order",
            PartnerUserId = partnerId,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            UpdatedAt = DateTimeOffset.UtcNow.AddDays(-1)
        });
        db.UserAuctionProfiles.Add(new UserAuctionProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ViolationStreak = 2,
            RestrictionType = AuctionRestrictionType.TempRestricted,
            RestrictionUntil = DateTimeOffset.UtcNow.AddDays(3),
            RestrictionReason = "Non-payment"
        });
        await db.SaveChangesAsync();

        var service = new InternalUserService(db, new MemoryCache(new MemoryCacheOptions()), NullLogger<InternalUserService>.Instance);

        // 1. Cooldown suppressed (delta == 0) still writes ReputationLog for idempotency
        var adj = await service.ApplyInternalReputationAdjustAsync(new InternalReputationAdjustRequest
        {
            UserId = userId,
            TargetRole = TargetRole.Buyer,
            ScoreDelta = 1,
            Reason = ReputationChangeReason.PositiveReview,
            ReferenceType = ReferenceType.Order,
            ReferenceId = "new-order-1",
            PartnerUserId = partnerId
        });
        Assert.True(adj.CooldownSuppressed);
        Assert.Equal(0, adj.AppliedDelta);
        Assert.True(await db.ReputationLogs.AnyAsync(l => l.ReferenceId == "new-order-1"));

        // 2. VerifyBidEligibilityAsync checks UserAuctionProfiles restriction
        var bidCheck = await service.VerifyBidEligibilityAsync(new VerifyBidEligibilityRequest
        {
            UserId = userId,
            AuctionId = Guid.NewGuid()
        });
        Assert.False(bidCheck.Allowed);
        Assert.Equal("AUCTION_TEMP_RESTRICTED", bidCheck.FailureReason);

        // 3. GetBatchSellerInfoAsync handles null request/lists safely
        var batchNull = await service.GetBatchSellerInfoAsync(null!);
        Assert.Empty(batchNull.Sellers);

        var batchEmptyLists = await service.GetBatchSellerInfoAsync(new BatchSellerInfoRequest { UserIds = null!, SellerIds = null! });
        Assert.Empty(batchEmptyLists.Sellers);
    }

    [Fact]
    public async Task AppDbContext_TryRevokeRefreshTokenAtomicAsync_InMemoryDoesNotFlushUnrelatedEntities()
    {
        using var db = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        db.Users.Add(new User { Id = userId, Email = "iso@nexus.local", FirstName = "I", LastName = "1", Status = UserStatus.Active });
        db.RefreshTokens.Add(new RefreshToken
        {
            Id = tokenId,
            UserId = userId,
            SessionId = Guid.NewGuid(),
            TokenHash = "hash-iso",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
            IsRevoked = false
        });
        await db.SaveChangesAsync();

        // Stage an unrelated pending entity on the caller's DbContext
        var stagedUserId = Guid.NewGuid();
        db.Users.Add(new User { Id = stagedUserId, Email = "staged@nexus.local", FirstName = "S", LastName = "2", Status = UserStatus.Active });

        var won = await db.TryRevokeRefreshTokenAtomicAsync(tokenId, DateTimeOffset.UtcNow);
        Assert.True(won);

        // Staged user should NOT have been flushed to the InMemory database by TryRevokeRefreshTokenAtomicAsync
        using var verifyDb = CreateInMemoryDbContext();
        Assert.Equal(EntityState.Added, db.Entry(db.Users.Local.First(u => u.Id == stagedUserId)).State);
    }

    private sealed class RecordingRedisOtpService : IRedisOtpService
    {
        public List<Guid> RevokedUserIds { get; } = new();
        public List<Guid> UnbannedUserIds { get; } = new();

        public Task SaveOtpAsync(string email, string code, TimeSpan validityDuration, TimeSpan retentionDuration, string purpose = "registration") => Task.CompletedTask;
        public Task<OtpEntry?> GetOtpAsync(string email, string purpose = "registration") => Task.FromResult<OtpEntry?>(null);
        public Task MarkOtpConsumedAsync(string email, string purpose = "registration") => Task.CompletedTask;
        public Task InvalidateExistingOtpsAsync(string email, string purpose = "registration") => Task.CompletedTask;
        public Task<int> IncrementLoginAttemptsAsync(string identifier, TimeSpan window) => Task.FromResult(1);
        public Task<bool> IsAccountLockedAsync(string identifier) => Task.FromResult(false);
        public Task LockAccountAsync(string identifier, TimeSpan duration) => Task.CompletedTask;
        public Task ResetLoginAttemptsAsync(string identifier) => Task.CompletedTask;
        public Task SaveDraftAsync(string key, string data, TimeSpan duration) => Task.CompletedTask;
        public Task<string?> GetDraftAsync(string key) => Task.FromResult<string?>(null);
        public Task<string?> GetAndDeleteDraftAsync(string key) => Task.FromResult<string?>(null);
        public Task<bool> IsCooldownActiveAsync(string email, string purpose = "registration") => Task.FromResult(false);
        public Task SetCooldownAsync(string email, TimeSpan duration, string purpose = "registration") => Task.CompletedTask;
        public Task<int> IncrementOtpAttemptsAsync(string email, TimeSpan window, string purpose = "registration") => Task.FromResult(1);
        public Task ResetOtpAttemptsAsync(string email, string purpose = "registration") => Task.CompletedTask;

        public Task RevokeUserTokensAsync(Guid userId, TimeSpan duration)
        {
            RevokedUserIds.Add(userId);
            return Task.CompletedTask;
        }

        public Task UnbanUserAsync(Guid userId)
        {
            UnbannedUserIds.Add(userId);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ReportService_ResolveReportAsync_DoesNotSuspendPendingSellerProfile()
    {
        using var db = CreateInMemoryDbContext();
        var reporterId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var reportId = Guid.NewGuid();

        db.Users.AddRange(
            new User { Id = reporterId, Email = "rep@nexus.local", FirstName = "R", LastName = "1", Status = UserStatus.Active },
            new User { Id = userId, Email = "pend@nexus.local", FirstName = "P", LastName = "1", Status = UserStatus.Active });
        db.SellerProfiles.Add(new SellerProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ShopName = "Pending Shop",
            Hotline = "0900",
            PickupAddress = "Addr",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "W1",
            Status = SellerProfileStatus.Pending
        });
        db.UserReports.Add(new UserReport
        {
            Id = reportId,
            ReporterId = reporterId,
            ReportedUserId = userId,
            TargetRole = TargetRole.Account,
            ReferenceType = ReferenceType.System,
            ViolationType = ReportViolationType.Fraud,
            Description = "Fraudulent account with pending seller profile.",
            Status = ReportStatus.Pending
        });
        await db.SaveChangesAsync();

        var service = new ReportService(db, NullLogger<ReportService>.Instance);
        await service.ResolveReportAsync(reportId, new ResolveReportRequest
        {
            TargetRole = TargetRole.Account,
            BanAccount = true,
            SuspendSellerShop = true,
            ResolutionNote = "Banning account; pending seller profile must remain Pending."
        }, staffId);

        var profile = await db.SellerProfiles.FirstAsync(sp => sp.UserId == userId);
        Assert.Equal(SellerProfileStatus.Pending, profile.Status);
    }

    [Fact]
    public async Task AdminUserService_UpdateUserAsync_RevokesRedisTokensOnDeleteAndClearsOnRestore()
    {
        using var db = CreateInMemoryDbContext();
        var buyerRole = new Role { Id = Guid.NewGuid(), RoleCode = RoleType.Buyer, RoleName = "Buyer" };
        var userId = Guid.NewGuid();
        db.Roles.Add(buyerRole);
        db.Users.Add(new User
        {
            Id = userId,
            Email = "deltest@nexus.local",
            FirstName = "Del",
            LastName = "Test",
            Status = UserStatus.Active,
            RoleId = buyerRole.Id,
            Role = buyerRole
        });
        await db.SaveChangesAsync();

        var redis = new RecordingRedisOtpService();
        var service = new AdminUserService(db, NullLogger<AdminUserService>.Instance, redisOtpService: redis);

        await service.UpdateUserAsync(userId, new UpdateAdminUserDto { Status = "Deleted", Reason = "Soft delete user" }, Guid.NewGuid());
        Assert.Contains(userId, redis.RevokedUserIds);

        await service.UpdateUserAsync(userId, new UpdateAdminUserDto { Status = "Active", Reason = "Restore user" }, Guid.NewGuid());
        Assert.Contains(userId, redis.UnbannedUserIds);
    }
}
