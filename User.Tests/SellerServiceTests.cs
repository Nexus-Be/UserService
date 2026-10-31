namespace User.Tests;

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using User.Application.Interfaces;
using User.Application.Models;
using User.Application.Services.Seller;
using User.Domain.Entities;
using User.Domain.Enums;
using User.Domain.Exceptions;
using User.Application.Common.Models;
using User.Infrastructure.Persistence;
using Xunit;

public sealed class SellerServiceTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"seller_test_db_{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    private sealed class FakeFulfillmentServiceClient : IFulfillmentServiceClient
    {
        public bool ShouldSucceed { get; set; } = true;
        public (Guid SellerId, Guid UserId, string ShopName, string ContactName, string ContactPhone, string PickupAddress, int ProvinceId, int DistrictId, string WardCode)? LastCreated { get; private set; }
        public Guid? LastDeletedSellerId { get; private set; }

        public Task<bool> CreateWarehouseAsync(
            Guid sellerId,
            Guid userId,
            string shopName,
            string contactName,
            string contactPhone,
            string pickupAddress,
            int provinceId,
            int districtId,
            string wardCode,
            CancellationToken cancellationToken = default)
        {
            LastCreated = (sellerId, userId, shopName, contactName, contactPhone, pickupAddress, provinceId, districtId, wardCode);
            return Task.FromResult(ShouldSucceed);
        }

        public Task<bool> UpdateWarehouseAsync(
            Guid sellerId,
            string shopName,
            string contactPhone,
            string pickupAddress,
            int provinceId,
            int districtId,
            string wardCode,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ShouldSucceed);
        }

        public Task<bool> DeleteWarehouseBySellerIdAsync(Guid sellerId, CancellationToken cancellationToken = default)
        {
            LastDeletedSellerId = sellerId;
            return Task.FromResult(ShouldSucceed);
        }
    }

    private sealed class FakeRedisOtpService : IRedisOtpService
    {
        public readonly ConcurrentDictionary<string, OtpEntry> Storage = new(StringComparer.OrdinalIgnoreCase);
        public readonly ConcurrentDictionary<string, string> Drafts = new(StringComparer.OrdinalIgnoreCase);
        public readonly ConcurrentDictionary<string, DateTimeOffset> Cooldowns = new(StringComparer.OrdinalIgnoreCase);
        public readonly ConcurrentDictionary<string, int> OtpAttempts = new(StringComparer.OrdinalIgnoreCase);

        private static string Key(string email, string purpose) => $"{purpose}:{email}";

        public Task SaveOtpAsync(string email, string code, TimeSpan validityDuration, TimeSpan retentionDuration, string purpose = "registration")
        {
            Storage[Key(email, purpose)] = new OtpEntry
            {
                Code = code,
                Email = email,
                Status = OtpStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.Add(validityDuration)
            };
            return Task.CompletedTask;
        }

        public Task<OtpEntry?> GetOtpAsync(string email, string purpose = "registration")
        {
            Storage.TryGetValue(Key(email, purpose), out var entry);
            return Task.FromResult(entry);
        }

        public Task MarkOtpConsumedAsync(string email, string purpose = "registration")
        {
            if (Storage.TryGetValue(Key(email, purpose), out var entry))
            {
                entry.Status = OtpStatus.Consumed;
                entry.ConsumedAt = DateTimeOffset.UtcNow;
            }
            return Task.CompletedTask;
        }

        public Task InvalidateExistingOtpsAsync(string email, string purpose = "registration")
        {
            Storage.TryRemove(Key(email, purpose), out _);
            OtpAttempts.TryRemove(Key(email, purpose), out _);
            return Task.CompletedTask;
        }

        public Task<bool> IsCooldownActiveAsync(string email, string purpose = "registration")
        {
            if (Cooldowns.TryGetValue(Key(email, purpose), out var expiresAt))
            {
                if (DateTimeOffset.UtcNow < expiresAt) return Task.FromResult(true);
                Cooldowns.TryRemove(Key(email, purpose), out _);
            }
            return Task.FromResult(false);
        }

        public Task SetCooldownAsync(string email, TimeSpan duration, string purpose = "registration")
        {
            Cooldowns[Key(email, purpose)] = DateTimeOffset.UtcNow.Add(duration);
            return Task.CompletedTask;
        }

        public Task<int> IncrementOtpAttemptsAsync(string email, TimeSpan window, string purpose = "registration")
        {
            var attempts = OtpAttempts.AddOrUpdate(Key(email, purpose), 1, (_, current) => current + 1);
            return Task.FromResult(attempts);
        }

        public Task ResetOtpAttemptsAsync(string email, string purpose = "registration")
        {
            OtpAttempts.TryRemove(Key(email, purpose), out _);
            return Task.CompletedTask;
        }

        public Task<int> IncrementLoginAttemptsAsync(string identifier, TimeSpan window) => Task.FromResult(1);
        public Task<bool> IsAccountLockedAsync(string identifier) => Task.FromResult(false);
        public Task LockAccountAsync(string identifier, TimeSpan duration) => Task.CompletedTask;
        public Task ResetLoginAttemptsAsync(string identifier) => Task.CompletedTask;

        public Task SaveDraftAsync(string key, string data, TimeSpan duration)
        {
            Drafts[key] = data;
            return Task.CompletedTask;
        }

        public Task<string?> GetDraftAsync(string key)
        {
            Drafts.TryGetValue(key, out var data);
            return Task.FromResult(data);
        }

        public Task<string?> GetAndDeleteDraftAsync(string key)
        {
            Drafts.TryRemove(key, out var data);
            return Task.FromResult(data);
        }
    }

    private static SellerService CreateSellerService(AppDbContext dbContext, IRedisOtpService? otp = null, IFulfillmentServiceClient? fulfillment = null) =>
        new(dbContext, otp ?? new FakeRedisOtpService(), null, fulfillment ?? new FakeFulfillmentServiceClient(), NullLogger<SellerService>.Instance);

    [Fact]
    public async Task RegisterSellerAsync_ValidInput_SavesDraftInRedis_AndVerifiesOtp_CreatesSellerProfile()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new FakeRedisOtpService();
        var fulfillmentService = new FakeFulfillmentServiceClient();
        var service = CreateSellerService(dbContext, otpService, fulfillmentService);

        var userId = Guid.NewGuid();
        var user = new Domain.Entities.User
        {
            Id = userId,
            Email = "buyer@nexus.local",
            PasswordHash = "hash",
            FirstName = "Van",
            LastName = "A",
            Status = UserStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var dto = new CreateSellerRegistrationDto
        {
            ShopName = "Green Leaf Tech",
            BusinessEmail = "contact@greenleaf.local",
            Hotline = "+84901234567",
            IdentityCardNumber = "079201001234",
            PickupAddress = "123 Nguyen Hue, Quan 1",
            ProvinceId = 202,
            DistrictId = 1442,
            WardCode = "20101"
        };

        // Step 1: Submit application (stores draft in Redis, NOT in DB)
        var response = await service.RegisterSellerAsync(userId, dto, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal("OTP_VERIFICATION_REQUIRED", response.Status);
        Assert.Equal("contact@greenleaf.local", response.BusinessEmail);
        Assert.Equal("VERIFY_OTP_VIA_POST_/api/v1/sellers/otp/verify", response.NextStep);
        Assert.Equal(300, response.OtpExpiresInSeconds);

        // Verify PostgreSQL is still 100% clean (no SellerProfile)
        var dbProfile = await dbContext.SellerProfiles.FirstOrDefaultAsync(s => s.UserId == userId);
        Assert.Null(dbProfile);

        // Verify Draft stored in Redis
        Assert.True(otpService.Drafts.ContainsKey($"user:seller_draft:{userId}"));

        // Verify OTP stored in Redis
        var savedOtp = await otpService.GetOtpAsync("contact@greenleaf.local", purpose: "seller_registration");
        Assert.NotNull(savedOtp);
        Assert.Equal(6, savedOtp.Code.Length);

        // Step 2: Verify OTP
        var verifyDto = new VerifySellerOtpDto
        {
            BusinessEmail = "contact@greenleaf.local",
            OtpCode = savedOtp.Code
        };

        var verifyResponse = await service.VerifySellerRegistrationOtpAsync(userId, verifyDto, CancellationToken.None);

        Assert.NotNull(verifyResponse);
        Assert.Equal("PENDING", verifyResponse.Status);
        Assert.Equal("Green Leaf Tech", verifyResponse.ShopName);
        Assert.Equal("contact@greenleaf.local", verifyResponse.BusinessEmail);

        // Verify DB now has SellerProfile with status PENDING
        var savedProfile = await dbContext.SellerProfiles.FirstOrDefaultAsync(s => s.UserId == userId);
        Assert.NotNull(savedProfile);
        Assert.Equal("Green Leaf Tech", savedProfile.ShopName);
        Assert.Equal("contact@greenleaf.local", savedProfile.BusinessEmail);
        Assert.Equal("+84901234567", savedProfile.Hotline);
        Assert.Equal(SellerProfileStatus.Pending, savedProfile.Status);

        // Verify IdentifyNumber was synced to User
        var updatedUser = await dbContext.Users.FindAsync(userId);
        Assert.Equal("079201001234", updatedUser!.IdentifyNumber);

        // Verify Draft was consumed/deleted from Redis
        Assert.False(otpService.Drafts.ContainsKey($"user:seller_draft:{userId}"));
    }

    [Fact]
    public async Task RegisterSellerAsync_MissingPayload_ThrowsValidationException()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = CreateSellerService(dbContext);

        var ex = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            service.RegisterSellerAsync(Guid.NewGuid(), null!, CancellationToken.None));

        Assert.Equal("VAL_INVALID_INPUT", ex.ErrorCode);
    }

    [Fact]
    public async Task RegisterSellerAsync_EmptyUserId_ThrowsValidationException()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = CreateSellerService(dbContext);

        var dto = new CreateSellerRegistrationDto
        {
            ShopName = "Test Shop",
            BusinessEmail = "shop@test.com",
            Hotline = "0901234567",
            IdentityCardNumber = "0123456789",
            PickupAddress = "123 Street",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "01"
        };

        var ex = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            service.RegisterSellerAsync(Guid.Empty, dto, CancellationToken.None));

        Assert.Equal("VAL_INVALID_INPUT", ex.ErrorCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData(null)]
    public async Task RegisterSellerAsync_InvalidShopName_ThrowsValidationException(string? invalidShopName)
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = CreateSellerService(dbContext);

        var dto = new CreateSellerRegistrationDto
        {
            ShopName = invalidShopName,
            BusinessEmail = "shop@test.com",
            Hotline = "0901234567",
            IdentityCardNumber = "0123456789",
            PickupAddress = "123 Street",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "01"
        };

        var ex = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            service.RegisterSellerAsync(Guid.NewGuid(), dto, CancellationToken.None));

        Assert.Equal("VAL_INVALID_SHOP_NAME", ex.ErrorCode);
    }

    [Theory]
    [InlineData("Nexus Store Official")]
    [InlineData("Admin Support")]
    [InlineData("Shopee Mall Vietnam")]
    public async Task RegisterSellerAsync_BannedShopName_ThrowsValidationException(string bannedName)
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = CreateSellerService(dbContext);

        var dto = new CreateSellerRegistrationDto
        {
            ShopName = bannedName,
            BusinessEmail = "shop@test.com",
            Hotline = "0901234567",
            IdentityCardNumber = "0123456789",
            PickupAddress = "123 Street",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "01"
        };

        var ex = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            service.RegisterSellerAsync(Guid.NewGuid(), dto, CancellationToken.None));

        Assert.Equal("VAL_BANNED_SHOP_NAME", ex.ErrorCode);
    }

    [Fact]
    public async Task RegisterSellerAsync_MissingGhnCodes_ThrowsValidationException()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = CreateSellerService(dbContext);

        var dto = new CreateSellerRegistrationDto
        {
            ShopName = "Valid Shop Name",
            BusinessEmail = "shop@test.com",
            Hotline = "0901234567",
            IdentityCardNumber = "0123456789",
            PickupAddress = "123 Street",
            ProvinceId = 0,
            DistrictId = 1,
            WardCode = "01"
        };

        var ex = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            service.RegisterSellerAsync(Guid.NewGuid(), dto, CancellationToken.None));

        Assert.Equal("VAL_INVALID_GHN_CODE", ex.ErrorCode);
    }

    [Fact]
    public async Task RegisterSellerAsync_UserNotFound_ThrowsDomainException()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = CreateSellerService(dbContext);

        var dto = new CreateSellerRegistrationDto
        {
            ShopName = "Valid Shop",
            BusinessEmail = "shop@test.com",
            Hotline = "0901234567",
            IdentityCardNumber = "0123456789",
            PickupAddress = "123 Street",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "01"
        };

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.RegisterSellerAsync(Guid.NewGuid(), dto, CancellationToken.None));

        Assert.Equal("USER_NOT_FOUND", ex.ErrorCode);
    }

    [Fact]
    public async Task RegisterSellerAsync_UserBanned_ThrowsDomainException()
    {
        using var dbContext = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        dbContext.Users.Add(new Domain.Entities.User
        {
            Id = userId,
            Email = "banned@nexus.local",
            PasswordHash = "hash",
            FirstName = "Banned",
            LastName = "User",
            Status = UserStatus.Banned
        });
        await dbContext.SaveChangesAsync();

        var service = CreateSellerService(dbContext);
        var dto = new CreateSellerRegistrationDto
        {
            ShopName = "Valid Shop",
            BusinessEmail = "shop@test.com",
            Hotline = "0901234567",
            IdentityCardNumber = "0123456789",
            PickupAddress = "123 Street",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "01"
        };

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.RegisterSellerAsync(userId, dto, CancellationToken.None));

        Assert.Equal("USER_BANNED", ex.ErrorCode);
    }

    [Fact]
    public async Task RegisterSellerAsync_AlreadyApprovedSeller_ThrowsDomainException()
    {
        using var dbContext = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var user = new Domain.Entities.User
        {
            Id = userId,
            Email = "seller@nexus.local",
            PasswordHash = "hash",
            FirstName = "Active",
            LastName = "Seller",
            Status = UserStatus.Active
        };
        dbContext.Users.Add(user);

        dbContext.SellerProfiles.Add(new SellerProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ShopName = "Existing Shop",
            Hotline = "0901234567",
            PickupAddress = "123 Street",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "01",
            Status = SellerProfileStatus.Approved
        });
        await dbContext.SaveChangesAsync();

        var service = CreateSellerService(dbContext);
        var dto = new CreateSellerRegistrationDto
        {
            ShopName = "New Shop Attempt",
            BusinessEmail = "shop@test.com",
            Hotline = "0901234567",
            IdentityCardNumber = "0123456789",
            PickupAddress = "123 Street",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "01"
        };

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.RegisterSellerAsync(userId, dto, CancellationToken.None));

        Assert.Equal("ALREADY_SELLER", ex.ErrorCode);
    }

    [Fact]
    public async Task RegisterSellerAsync_RequestAlreadyPending_ThrowsDomainException()
    {
        using var dbContext = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var user = new Domain.Entities.User
        {
            Id = userId,
            Email = "pending@nexus.local",
            PasswordHash = "hash",
            FirstName = "Pending",
            LastName = "Applicant",
            Status = UserStatus.Active
        };
        dbContext.Users.Add(user);

        dbContext.SellerProfiles.Add(new SellerProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ShopName = "Pending Shop",
            Hotline = "0901234567",
            PickupAddress = "123 Street",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "01",
            Status = SellerProfileStatus.Pending
        });
        await dbContext.SaveChangesAsync();

        var service = CreateSellerService(dbContext);
        var dto = new CreateSellerRegistrationDto
        {
            ShopName = "Another Shop",
            BusinessEmail = "shop@test.com",
            Hotline = "0901234567",
            IdentityCardNumber = "0123456789",
            PickupAddress = "123 Street",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "01"
        };

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.RegisterSellerAsync(userId, dto, CancellationToken.None));

        Assert.Equal("REQUEST_ALREADY_PENDING", ex.ErrorCode);
    }

    [Fact]
    public async Task RegisterSellerAsync_ShopNameTaken_ThrowsDomainException()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otherUserId = Guid.NewGuid();
        dbContext.SellerProfiles.Add(new SellerProfile
        {
            Id = Guid.NewGuid(),
            UserId = otherUserId,
            ShopName = "TakenShop",
            Hotline = "0901234567",
            PickupAddress = "123 Street",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "01",
            Status = SellerProfileStatus.Approved
        });

        var userId = Guid.NewGuid();
        dbContext.Users.Add(new Domain.Entities.User
        {
            Id = userId,
            Email = "applicant@nexus.local",
            PasswordHash = "hash",
            FirstName = "Test",
            LastName = "User",
            Status = UserStatus.Active
        });
        await dbContext.SaveChangesAsync();

        var service = CreateSellerService(dbContext);
        var dto = new CreateSellerRegistrationDto
        {
            ShopName = "takenshop",
            BusinessEmail = "shop@test.com",
            Hotline = "0901234567",
            IdentityCardNumber = "0123456789",
            PickupAddress = "123 Street",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "01"
        };

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.RegisterSellerAsync(userId, dto, CancellationToken.None));

        Assert.Equal("SHOP_NAME_TAKEN", ex.ErrorCode);
    }

    [Fact]
    public async Task RegisterSellerAsync_DuplicateIdentityCard_ThrowsDomainException()
    {
        using var dbContext = CreateInMemoryDbContext();
        var existingUserId = Guid.NewGuid();
        var existingUser = new Domain.Entities.User
        {
            Id = existingUserId,
            Email = "existing@shop.local",
            PasswordHash = "hash",
            FirstName = "Old",
            LastName = "Seller",
            IdentifyNumber = "079201009999",
            Status = UserStatus.Active
        };
        dbContext.Users.Add(existingUser);
        dbContext.SellerProfiles.Add(new SellerProfile
        {
            Id = Guid.NewGuid(),
            UserId = existingUserId,
            ShopName = "First Shop",
            Hotline = "0901",
            PickupAddress = "Street 1",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "01",
            Status = SellerProfileStatus.Approved
        });

        var newUserId = Guid.NewGuid();
        dbContext.Users.Add(new Domain.Entities.User
        {
            Id = newUserId,
            Email = "fraud@shop.local",
            PasswordHash = "hash",
            FirstName = "New",
            LastName = "Candidate",
            Status = UserStatus.Active
        });
        await dbContext.SaveChangesAsync();

        var service = CreateSellerService(dbContext);
        var dto = new CreateSellerRegistrationDto
        {
            ShopName = "Second Shop",
            BusinessEmail = "second@shop.local",
            Hotline = "0902",
            IdentityCardNumber = "079201009999",
            PickupAddress = "Street 2",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "01"
        };

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.RegisterSellerAsync(newUserId, dto, CancellationToken.None));

        Assert.Equal("IDENTITY_CARD_ALREADY_USED", ex.ErrorCode);
    }

    [Fact]
    public async Task RegisterSellerAsync_ReputationTooLow_ThrowsDomainException()
    {
        using var dbContext = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var user = new Domain.Entities.User
        {
            Id = userId,
            Email = "lowrep@nexus.local",
            PasswordHash = "hash",
            FirstName = "Low",
            LastName = "Rep",
            Status = UserStatus.Active
        };
        dbContext.Users.Add(user);

        dbContext.ReputationScores.Add(new ReputationScore
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CurrentScore = 40
        });
        await dbContext.SaveChangesAsync();

        var service = CreateSellerService(dbContext);
        var dto = new CreateSellerRegistrationDto
        {
            ShopName = "Low Rep Shop",
            BusinessEmail = "shop@test.com",
            Hotline = "0901234567",
            IdentityCardNumber = "0123456789",
            PickupAddress = "123 Street",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "01"
        };

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.RegisterSellerAsync(userId, dto, CancellationToken.None));

        Assert.Equal("REPUTATION_TOO_LOW", ex.ErrorCode);
    }

    [Fact]
    public async Task RegisterSellerAsync_ReapplyAfterRejected_ReusesAndUpdatesProfile()
    {
        using var dbContext = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var user = new Domain.Entities.User
        {
            Id = userId,
            Email = "rejected@nexus.local",
            PasswordHash = "hash",
            FirstName = "Rejected",
            LastName = "User",
            Status = UserStatus.Active
        };
        dbContext.Users.Add(user);

        var existingProfileId = Guid.NewGuid();
        dbContext.SellerProfiles.Add(new SellerProfile
        {
            Id = existingProfileId,
            UserId = userId,
            ShopName = "Old Rejected Shop",
            Hotline = "0901234567",
            PickupAddress = "Old Address",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "01",
            Status = SellerProfileStatus.Rejected,
            RejectionReason = "Missing documentation"
        });
        await dbContext.SaveChangesAsync();

        var otpService = new FakeRedisOtpService();
        var service = CreateSellerService(dbContext, otpService);

        var dto = new CreateSellerRegistrationDto
        {
            ShopName = "New Better Shop",
            BusinessEmail = "new@shop.com",
            Hotline = "+84909999999",
            IdentityCardNumber = "079090001234",
            PickupAddress = "456 Le Loi",
            ProvinceId = 202,
            DistrictId = 1442,
            WardCode = "20101"
        };

        // Step 1: Submits draft
        var response = await service.RegisterSellerAsync(userId, dto, CancellationToken.None);
        Assert.Equal("OTP_VERIFICATION_REQUIRED", response.Status);

        var savedOtp = await otpService.GetOtpAsync("new@shop.com", "seller_registration");
        Assert.NotNull(savedOtp);

        // Step 2: Verifies OTP -> should update the existing record without duplicate key exception
        var verifyDto = new VerifySellerOtpDto { BusinessEmail = "new@shop.com", OtpCode = savedOtp.Code };
        var verifyResp = await service.VerifySellerRegistrationOtpAsync(userId, verifyDto, CancellationToken.None);

        Assert.Equal(existingProfileId.ToString(), verifyResp.SellerId);
        Assert.Equal("PENDING", verifyResp.Status);

        var updatedProfile = await dbContext.SellerProfiles.FirstOrDefaultAsync(s => s.UserId == userId);
        Assert.NotNull(updatedProfile);
        Assert.Equal(existingProfileId, updatedProfile.Id);
        Assert.Equal("New Better Shop", updatedProfile.ShopName);
        Assert.Equal(SellerProfileStatus.Pending, updatedProfile.Status);
        Assert.Null(updatedProfile.RejectionReason);
    }

    [Fact]
    public async Task VerifySellerRegistrationOtpAsync_InvalidOtp_ThrowsDomainException()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new FakeRedisOtpService();
        var service = CreateSellerService(dbContext, otpService);

        var userId = Guid.NewGuid();
        dbContext.Users.Add(new Domain.Entities.User
        {
            Id = userId,
            Email = "user@test.local",
            PasswordHash = "hash",
            FirstName = "A",
            LastName = "B",
            Status = UserStatus.Active
        });
        await dbContext.SaveChangesAsync();

        var dto = new CreateSellerRegistrationDto
        {
            ShopName = "Test Shop",
            BusinessEmail = "user@test.local",
            Hotline = "0901",
            IdentityCardNumber = "0123",
            PickupAddress = "123 Street",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "01"
        };
        await service.RegisterSellerAsync(userId, dto, CancellationToken.None);

        var verifyDto = new VerifySellerOtpDto { BusinessEmail = "user@test.local", OtpCode = "000000" };
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.VerifySellerRegistrationOtpAsync(userId, verifyDto, CancellationToken.None));

        Assert.Equal("OTP_INVALID", ex.ErrorCode);
    }

    [Fact]
    public async Task VerifySellerRegistrationOtpAsync_DoubleSubmit_SecondAttemptFailsFast()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new FakeRedisOtpService();
        var service = CreateSellerService(dbContext, otpService);

        var userId = Guid.NewGuid();
        dbContext.Users.Add(new Domain.Entities.User
        {
            Id = userId,
            Email = "double@test.local",
            PasswordHash = "hash",
            FirstName = "A",
            LastName = "B",
            Status = UserStatus.Active
        });
        await dbContext.SaveChangesAsync();

        var dto = new CreateSellerRegistrationDto
        {
            ShopName = "Test Double Shop",
            BusinessEmail = "double@test.local",
            Hotline = "0901",
            IdentityCardNumber = "0123",
            PickupAddress = "123 Street",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "01"
        };
        await service.RegisterSellerAsync(userId, dto, CancellationToken.None);

        var savedOtp = await otpService.GetOtpAsync("double@test.local", "seller_registration");
        var verifyDto = new VerifySellerOtpDto { BusinessEmail = "double@test.local", OtpCode = savedOtp!.Code };

        // Attempt 1: succeeds
        var resp1 = await service.VerifySellerRegistrationOtpAsync(userId, verifyDto, CancellationToken.None);
        Assert.Equal("PENDING", resp1.Status);

        // Attempt 2: fails fast because draft is atomically deleted and OTP marked consumed
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.VerifySellerRegistrationOtpAsync(userId, verifyDto, CancellationToken.None));

        Assert.True(ex.ErrorCode == "OTP_ALREADY_USED" || ex.ErrorCode == "OTP_INVALID" || ex.ErrorCode == "DRAFT_NOT_FOUND");
    }

    [Fact]
    public async Task ResendSellerOtpAsync_DraftExists_DispatchesNewOtp()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new FakeRedisOtpService();
        var service = CreateSellerService(dbContext, otpService);

        var userId = Guid.NewGuid();
        dbContext.Users.Add(new Domain.Entities.User
        {
            Id = userId,
            Email = "resend@test.local",
            PasswordHash = "hash",
            FirstName = "A",
            LastName = "B",
            Status = UserStatus.Active
        });
        await dbContext.SaveChangesAsync();

        var dto = new CreateSellerRegistrationDto
        {
            ShopName = "Resend Shop",
            BusinessEmail = "resend@test.local",
            Hotline = "0901",
            IdentityCardNumber = "0123",
            PickupAddress = "123 Street",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "01"
        };
        await service.RegisterSellerAsync(userId, dto, CancellationToken.None);

        await service.ResendSellerOtpAsync(userId, "resend@test.local", CancellationToken.None);

        var newOtp = await otpService.GetOtpAsync("resend@test.local", "seller_registration");
        Assert.NotNull(newOtp);
        Assert.Equal(6, newOtp.Code.Length);
    }

    [Fact]
    public async Task ResendSellerOtpAsync_CooldownActive_ThrowsDomainException()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new FakeRedisOtpService();
        var service = CreateSellerService(dbContext, otpService);

        var userId = Guid.NewGuid();
        dbContext.Users.Add(new Domain.Entities.User
        {
            Id = userId,
            Email = "seller.cooldown@test.local",
            PasswordHash = "hash",
            FirstName = "Seller",
            LastName = "Cooldown",
            Status = UserStatus.Active
        });
        await dbContext.SaveChangesAsync();

        var dto = new CreateSellerRegistrationDto
        {
            ShopName = "Cooldown Shop",
            BusinessEmail = "seller.cooldown@test.local",
            Hotline = "0901",
            IdentityCardNumber = "0123",
            PickupAddress = "123 Street",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "01"
        };
        await service.RegisterSellerAsync(userId, dto, CancellationToken.None);

        // First resend succeeds and sets cooldown
        await service.ResendSellerOtpAsync(userId, "seller.cooldown@test.local", CancellationToken.None);

        // Immediate second resend throws OTP_COOLDOWN_ACTIVE (HTTP 429)
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.ResendSellerOtpAsync(userId, "seller.cooldown@test.local", CancellationToken.None));

        Assert.Equal("OTP_COOLDOWN_ACTIVE", ex.ErrorCode);
        Assert.Equal(429, ex.StatusCode);
    }

    [Fact]
    public async Task VerifySellerRegistrationOtpAsync_5IncorrectAttempts_ThrowsMaxAttemptsExceeded_AndDeletesDraft()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new FakeRedisOtpService();
        var service = CreateSellerService(dbContext, otpService);

        var userId = Guid.NewGuid();
        dbContext.Users.Add(new Domain.Entities.User
        {
            Id = userId,
            Email = "seller.brute@test.local",
            PasswordHash = "hash",
            FirstName = "Seller",
            LastName = "Brute",
            Status = UserStatus.Active
        });
        await dbContext.SaveChangesAsync();

        var dto = new CreateSellerRegistrationDto
        {
            ShopName = "Brute Shop",
            BusinessEmail = "seller.brute@test.local",
            Hotline = "0901",
            IdentityCardNumber = "0123",
            PickupAddress = "123 Street",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "01"
        };
        await service.RegisterSellerAsync(userId, dto, CancellationToken.None);

        var verifyDto = new VerifySellerOtpDto { BusinessEmail = "seller.brute@test.local", OtpCode = "000000" };

        // Attempts 1 to 4: OTP_INVALID
        for (var i = 1; i <= 4; i++)
        {
            var exInvalid = await Assert.ThrowsAsync<DomainException>(() =>
                service.VerifySellerRegistrationOtpAsync(userId, verifyDto, CancellationToken.None));
            Assert.Equal("OTP_INVALID", exInvalid.ErrorCode);
        }

        // Attempt 5: OTP_MAX_ATTEMPTS_EXCEEDED
        var exExceeded = await Assert.ThrowsAsync<DomainException>(() =>
            service.VerifySellerRegistrationOtpAsync(userId, verifyDto, CancellationToken.None));
        Assert.Equal("OTP_MAX_ATTEMPTS_EXCEEDED", exExceeded.ErrorCode);

        // Subsequent attempt: OTP_EXPIRED (because OTP was invalidated)
        var exExpired = await Assert.ThrowsAsync<DomainException>(() =>
            service.VerifySellerRegistrationOtpAsync(userId, verifyDto, CancellationToken.None));
        Assert.Equal("OTP_EXPIRED", exExpired.ErrorCode);

        // Draft was also deleted
        var draft = await otpService.GetDraftAsync($"user:seller_draft:{userId}");
        Assert.Null(draft);
    }

    [Fact]
    public async Task GetSellerRegistrationStatusAsync_ReturnsAccurateWorkflowStates()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new FakeRedisOtpService();
        var service = CreateSellerService(dbContext, otpService);

        var userId = Guid.NewGuid();
        dbContext.Users.Add(new Domain.Entities.User
        {
            Id = userId,
            Email = "workflow@test.local",
            PasswordHash = "hash",
            FirstName = "A",
            LastName = "B",
            Status = UserStatus.Active
        });
        await dbContext.SaveChangesAsync();

        // 1. Not registered
        var status1 = await service.GetSellerRegistrationStatusAsync(userId, CancellationToken.None);
        Assert.Equal("NOT_REGISTERED", status1.Status);

        // 2. Draft saved
        var dto = new CreateSellerRegistrationDto
        {
            ShopName = "Workflow Shop",
            BusinessEmail = "workflow@test.local",
            Hotline = "0901",
            IdentityCardNumber = "0123",
            PickupAddress = "123 Street",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "01"
        };
        await service.RegisterSellerAsync(userId, dto, CancellationToken.None);

        var status2 = await service.GetSellerRegistrationStatusAsync(userId, CancellationToken.None);
        Assert.Equal("OTP_VERIFICATION_REQUIRED", status2.Status);

        // 3. OTP verified -> PENDING
        var otp = await otpService.GetOtpAsync("workflow@test.local", "seller_registration");
        await service.VerifySellerRegistrationOtpAsync(userId, new VerifySellerOtpDto { BusinessEmail = "workflow@test.local", OtpCode = otp!.Code }, CancellationToken.None);

        var status3 = await service.GetSellerRegistrationStatusAsync(userId, CancellationToken.None);
        Assert.Equal("PENDING", status3.Status);
    }

    [Fact]
    public async Task GetSellerRegistrationsAsync_ReturnsPagedItems_WithStatusFilter()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = CreateSellerService(dbContext);

        var user1 = new Domain.Entities.User { Id = Guid.NewGuid(), Email = "u1@test.com", FirstName = "User", LastName = "One", PasswordHash = "hash", Status = UserStatus.Active };
        var user2 = new Domain.Entities.User { Id = Guid.NewGuid(), Email = "u2@test.com", FirstName = "User", LastName = "Two", PasswordHash = "hash", Status = UserStatus.Active };
        dbContext.Users.AddRange(user1, user2);

        dbContext.SellerProfiles.Add(new SellerProfile
        {
            Id = Guid.NewGuid(),
            UserId = user1.Id,
            ShopName = "Pending Shop",
            Hotline = "0901",
            PickupAddress = "Address 1",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "01",
            Status = SellerProfileStatus.Pending
        });

        dbContext.SellerProfiles.Add(new SellerProfile
        {
            Id = Guid.NewGuid(),
            UserId = user2.Id,
            ShopName = "Approved Shop",
            Hotline = "0902",
            PickupAddress = "Address 2",
            ProvinceId = 2,
            DistrictId = 2,
            WardCode = "02",
            Status = SellerProfileStatus.Approved
        });

        await dbContext.SaveChangesAsync();

        var result = await service.GetSellerRegistrationsAsync(1, 10, "PENDING", CancellationToken.None);

        Assert.Equal(1, result.Pagination.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("PENDING", result.Items[0].Status);
        Assert.Equal("Pending Shop", result.Items[0].ShopName);
    }

    [Fact]
    public async Task ApproveSellerRegistrationAsync_PendingProfile_CallsFulfillmentDownstreamFirst_GrantsSellerRole_AndRecordsAuditLog()
    {
        using var dbContext = CreateInMemoryDbContext();
        var fakeFulfillment = new FakeFulfillmentServiceClient();
        var service = CreateSellerService(dbContext, fulfillment: fakeFulfillment);

        var roleId = Guid.NewGuid();
        dbContext.Roles.Add(new Role
        {
            Id = roleId,
            RoleCode = RoleType.Seller,
            RoleName = "Seller"
        });

        var userId = Guid.NewGuid();
        var user = new Domain.Entities.User
        {
            Id = userId,
            Email = "candidate@test.com",
            FirstName = "Van",
            LastName = "B",
            PasswordHash = "hash",
            Status = UserStatus.Active
        };
        dbContext.Users.Add(user);

        var regId = Guid.NewGuid();
        var profile = new SellerProfile
        {
            Id = regId,
            UserId = userId,
            ShopName = "Approved Candidate Shop",
            Hotline = "0901234567",
            PickupAddress = "Address 123",
            ProvinceId = 202,
            DistrictId = 1442,
            WardCode = "20101",
            Status = SellerProfileStatus.Pending
        };
        dbContext.SellerProfiles.Add(profile);
        await dbContext.SaveChangesAsync();

        var staffId = Guid.NewGuid();
        var response = await service.ApproveSellerRegistrationAsync(regId, staffId, new ApproveSellerRegistrationDto { Note = "Valid documents" }, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(regId.ToString(), response.RegistrationId);
        Assert.Equal(userId.ToString(), response.UserId);
        Assert.Equal("APPROVED", response.Status);

        // Verify Downstream-First was called with correct data
        Assert.NotNull(fakeFulfillment.LastCreated);
        Assert.Equal(regId, fakeFulfillment.LastCreated.Value.SellerId);
        Assert.Equal("Approved Candidate Shop", fakeFulfillment.LastCreated.Value.ShopName);
        Assert.Equal("Van B", fakeFulfillment.LastCreated.Value.ContactName);
        Assert.Equal("0901234567", fakeFulfillment.LastCreated.Value.ContactPhone);

        var updatedProfile = await dbContext.SellerProfiles.FindAsync(regId);
        Assert.Equal(SellerProfileStatus.Approved, updatedProfile!.Status);
        Assert.Equal(staffId, updatedProfile.ReviewedBy);
        Assert.NotNull(updatedProfile.ReviewedAt);

        // Verify Role SELLER was granted to user
        var userRoles = await dbContext.UserRoles.Where(ur => ur.UserId == userId).ToListAsync();
        Assert.Contains(userRoles, ur => ur.RoleId == roleId);

        // Verify AuditLog
        var audit = await dbContext.AuditLogs.FirstOrDefaultAsync(a => a.EntityId == regId.ToString() && a.Action == AuditAction.SellerApprove);
        Assert.NotNull(audit);
        Assert.Equal(staffId.ToString(), audit.UserId);
    }

    [Fact]
    public async Task ApproveSellerRegistrationAsync_WhenFulfillmentDownstreamFails_Throws502_AndLeavesDbUntouched()
    {
        using var dbContext = CreateInMemoryDbContext();
        var fakeFulfillment = new FakeFulfillmentServiceClient { ShouldSucceed = false };
        var service = CreateSellerService(dbContext, fulfillment: fakeFulfillment);

        var roleId = Guid.NewGuid();
        dbContext.Roles.Add(new Role
        {
            Id = roleId,
            RoleCode = RoleType.Seller,
            RoleName = "Seller"
        });

        var userId = Guid.NewGuid();
        var user = new Domain.Entities.User
        {
            Id = userId,
            Email = "fail_downstream@test.com",
            FirstName = "Van",
            LastName = "B",
            PasswordHash = "hash",
            Status = UserStatus.Active
        };
        dbContext.Users.Add(user);

        var regId = Guid.NewGuid();
        var profile = new SellerProfile
        {
            Id = regId,
            UserId = userId,
            ShopName = "Downstream Fail Shop",
            Hotline = "0901234567",
            PickupAddress = "Address 123",
            ProvinceId = 202,
            DistrictId = 1442,
            WardCode = "20101",
            Status = SellerProfileStatus.Pending
        };
        dbContext.SellerProfiles.Add(profile);
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.ApproveSellerRegistrationAsync(regId, Guid.NewGuid(), null, CancellationToken.None));

        Assert.Equal("ERR_FULFILLMENT_UNAVAILABLE", ex.ErrorCode);
        Assert.Equal(502, ex.StatusCode);

        // Verify local DB status remains PENDING
        var unchangedProfile = await dbContext.SellerProfiles.FindAsync(regId);
        Assert.Equal(SellerProfileStatus.Pending, unchangedProfile!.Status);

        // Verify role Seller was NOT granted
        var userRoles = await dbContext.UserRoles.Where(ur => ur.UserId == userId).ToListAsync();
        Assert.DoesNotContain(userRoles, ur => ur.RoleId == roleId);
    }

    [Fact]
    public async Task ApproveSellerRegistrationAsync_NotFound_ThrowsDomainException()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = CreateSellerService(dbContext);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.ApproveSellerRegistrationAsync(Guid.NewGuid(), Guid.NewGuid(), null, CancellationToken.None));

        Assert.Equal("REGISTRATION_NOT_FOUND", ex.ErrorCode);
    }

    [Fact]
    public async Task ApproveSellerRegistrationAsync_AlreadyApproved_ThrowsDomainException()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = CreateSellerService(dbContext);

        var userId = Guid.NewGuid();
        dbContext.Users.Add(new Domain.Entities.User
        {
            Id = userId,
            Email = "already-approved@test.com",
            FirstName = "Van",
            LastName = "A",
            PasswordHash = "hash",
            Status = UserStatus.Active
        });

        var regId = Guid.NewGuid();
        dbContext.SellerProfiles.Add(new SellerProfile
        {
            Id = regId,
            UserId = userId,
            ShopName = "Already Approved Shop",
            Hotline = "0901234567",
            PickupAddress = "Address 123",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "01",
            Status = SellerProfileStatus.Approved
        });
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.ApproveSellerRegistrationAsync(regId, Guid.NewGuid(), null, CancellationToken.None));

        Assert.Equal("ALREADY_APPROVED", ex.ErrorCode);
    }

    [Fact]
    public async Task RejectSellerRegistrationAsync_ValidReason_UpdatesStatusToRejected_AndRecordsAuditLog()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = CreateSellerService(dbContext);

        var userId = Guid.NewGuid();
        var regId = Guid.NewGuid();
        dbContext.SellerProfiles.Add(new SellerProfile
        {
            Id = regId,
            UserId = userId,
            ShopName = "To Be Rejected Shop",
            Hotline = "0901234567",
            PickupAddress = "Address 123",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "01",
            Status = SellerProfileStatus.Pending
        });
        await dbContext.SaveChangesAsync();

        var staffId = Guid.NewGuid();
        var response = await service.RejectSellerRegistrationAsync(regId, staffId, new RejectSellerRegistrationDto { RejectionReason = "Missing Business License" }, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal("REJECTED", response.Status);
        Assert.Equal("Missing Business License", response.RejectionReason);

        var updatedProfile = await dbContext.SellerProfiles.FindAsync(regId);
        Assert.Equal(SellerProfileStatus.Rejected, updatedProfile!.Status);
        Assert.Equal("Missing Business License", updatedProfile.RejectionReason);
        Assert.Equal(staffId, updatedProfile.ReviewedBy);
    }

    [Fact]
    public async Task RejectSellerRegistrationAsync_MissingReason_ThrowsValidationException()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = CreateSellerService(dbContext);

        var ex = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            service.RejectSellerRegistrationAsync(Guid.NewGuid(), Guid.NewGuid(), new RejectSellerRegistrationDto { RejectionReason = "" }, CancellationToken.None));

        Assert.Equal("VAL_INVALID_INPUT", ex.ErrorCode);
    }

    [Fact]
    public async Task GetSellerOwnProfileAsync_WhenProfileExists_ReturnsProfileDataWithReputationAndRating()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = CreateSellerService(dbContext);

        var userId = Guid.NewGuid();
        var user = new Domain.Entities.User
        {
            Id = userId,
            Email = "owner@test.com",
            FirstName = "Shop",
            LastName = "Owner",
            PasswordHash = "hash",
            Status = UserStatus.Active
        };
        dbContext.Users.Add(user);

        dbContext.ReputationScores.Add(new ReputationScore
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CurrentScore = 85,
            TrustLevel = TrustLevel.Good
        });

        var profileId = Guid.NewGuid();
        dbContext.SellerProfiles.Add(new SellerProfile
        {
            Id = profileId,
            UserId = userId,
            ShopName = "High Rep Shop",
            BusinessEmail = "owner@shop.com",
            Hotline = "0901234567",
            PickupAddress = "Street 1",
            ProvinceId = 202,
            DistrictId = 1442,
            WardCode = "20101",
            Status = SellerProfileStatus.Approved
        });
        await dbContext.SaveChangesAsync();

        var result = await service.GetSellerOwnProfileAsync(userId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(profileId.ToString(), result.SellerId);
        Assert.Equal("High Rep Shop", result.ShopName);
        Assert.Equal("owner@shop.com", result.BusinessEmail);
        Assert.Equal(85, result.ReputationScore);
        Assert.Equal(TrustLevel.Good.ToString(), result.TrustLevel);
    }

    [Fact]
    public async Task GetSellerOwnProfileAsync_WhenProfileNotFound_ThrowsDomainException()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = CreateSellerService(dbContext);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.GetSellerOwnProfileAsync(Guid.NewGuid(), CancellationToken.None));

        Assert.Equal("SELLER_NOT_FOUND", ex.ErrorCode);
    }

    [Fact]
    public async Task GetSellerOwnProfileAsync_WhenUserIdEmpty_ThrowsValidationException()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = CreateSellerService(dbContext);

        var ex = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            service.GetSellerOwnProfileAsync(Guid.Empty, CancellationToken.None));

        Assert.Equal("VAL_INVALID_INPUT", ex.ErrorCode);
    }

    [Fact]
    public async Task UpdateSellerOwnProfileAsync_ValidData_UpdatesProfileAndLogsAudit()
    {
        using var dbContext = CreateInMemoryDbContext();
        var fakeFulfillment = new FakeFulfillmentServiceClient();
        var service = CreateSellerService(dbContext, fulfillment: fakeFulfillment);

        var userId = Guid.NewGuid();
        var user = new Domain.Entities.User
        {
            Id = userId,
            Email = "update_seller@test.com",
            FirstName = "Shop",
            LastName = "Owner",
            PasswordHash = "hash",
            Status = UserStatus.Active
        };
        dbContext.Users.Add(user);

        var profileId = Guid.NewGuid();
        dbContext.SellerProfiles.Add(new SellerProfile
        {
            Id = profileId,
            UserId = userId,
            ShopName = "Original Shop Name",
            BusinessEmail = "old@shop.com",
            Hotline = "0901111111",
            PickupAddress = "Old Address",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "01",
            Status = SellerProfileStatus.Approved
        });
        await dbContext.SaveChangesAsync();

        var updateDto = new UpdateSellerOwnProfileDto
        {
            ShopName = "Updated Shop Name",
            Hotline = "0902222222",
            PickupAddress = "New Pickup Address"
        };

        var response = await service.UpdateSellerOwnProfileAsync(userId, updateDto, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal("Updated Shop Name", response.ShopName);

        var updated = await dbContext.SellerProfiles.FindAsync(profileId);
        Assert.Equal("Updated Shop Name", updated!.ShopName);
        Assert.Equal("0902222222", updated.Hotline);
        Assert.Equal("New Pickup Address", updated.PickupAddress);

        // Verify AuditLog
        var audit = await dbContext.AuditLogs.FirstOrDefaultAsync(a => a.EntityId == profileId.ToString() && a.Action == AuditAction.UserUpdate);
        Assert.NotNull(audit);
    }

    [Fact]
    public async Task UpdateSellerOwnProfileAsync_DuplicateShopName_ThrowsDomainException()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = CreateSellerService(dbContext);

        var otherUserId = Guid.NewGuid();
        dbContext.SellerProfiles.Add(new SellerProfile
        {
            Id = Guid.NewGuid(),
            UserId = otherUserId,
            ShopName = "Existing Brand Shop",
            Hotline = "0901",
            PickupAddress = "Address 1",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "01",
            Status = SellerProfileStatus.Approved
        });

        var userId = Guid.NewGuid();
        var user = new Domain.Entities.User
        {
            Id = userId,
            Email = "my@shop.local",
            FirstName = "A",
            LastName = "B",
            PasswordHash = "hash",
            Status = UserStatus.Active
        };
        dbContext.Users.Add(user);

        var profileId = Guid.NewGuid();
        dbContext.SellerProfiles.Add(new SellerProfile
        {
            Id = profileId,
            UserId = userId,
            ShopName = "My Original Shop",
            Hotline = "0902",
            PickupAddress = "Address 2",
            ProvinceId = 2,
            DistrictId = 2,
            WardCode = "02",
            Status = SellerProfileStatus.Approved
        });
        await dbContext.SaveChangesAsync();

        var updateDto = new UpdateSellerOwnProfileDto
        {
            ShopName = "Existing Brand Shop"
        };

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.UpdateSellerOwnProfileAsync(userId, updateDto, CancellationToken.None));

        Assert.Equal("SHOP_NAME_TAKEN", ex.ErrorCode);
    }

    [Fact]
    public async Task UpdateSellerOwnProfileAsync_BannedUser_ThrowsDomainException()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = CreateSellerService(dbContext);

        var userId = Guid.NewGuid();
        var user = new Domain.Entities.User
        {
            Id = userId,
            Email = "banned_seller@test.com",
            FirstName = "Banned",
            LastName = "Seller",
            PasswordHash = "hash",
            Status = UserStatus.Banned
        };
        dbContext.Users.Add(user);

        dbContext.SellerProfiles.Add(new SellerProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ShopName = "Banned Shop",
            Hotline = "0901",
            PickupAddress = "Address 1",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "01",
            Status = SellerProfileStatus.Approved
        });
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.UpdateSellerOwnProfileAsync(userId, new UpdateSellerOwnProfileDto { ShopName = "Try To Change" }, CancellationToken.None));

        Assert.Equal("USER_BANNED", ex.ErrorCode);
    }

    [Fact]
    public async Task UpdateSellerOwnProfileAsync_ProfileNotFound_ThrowsDomainException()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = CreateSellerService(dbContext);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.UpdateSellerOwnProfileAsync(Guid.NewGuid(), new UpdateSellerOwnProfileDto { ShopName = "Valid Name" }, CancellationToken.None));

        Assert.Equal("SELLER_NOT_FOUND", ex.ErrorCode);
    }
}
