namespace User.Tests;

using System;
using System.Collections.Concurrent;
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

    private sealed class FakeRedisOtpService : IRedisOtpService
    {
        public readonly ConcurrentDictionary<string, OtpEntry> Storage = new(StringComparer.OrdinalIgnoreCase);

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
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task RegisterSellerAsync_ValidInput_CreatesSellerProfileAndDispatchesOtp()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new FakeRedisOtpService();
        var service = new SellerService(dbContext, otpService, null, NullLogger<SellerService>.Instance);

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
            ShopName = "Nexus Official Store",
            BusinessEmail = "business@nexus.local",
            Hotline = "+84901234567",
            IdentityCardNumber = "079201001234",
            PickupAddress = "123 Nguyen Hue, Quan 1",
            ProvinceId = 202,
            DistrictId = 1442,
            WardCode = "20101"
        };

        var response = await service.RegisterSellerAsync(userId, dto, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal("PENDING", response.Status);
        Assert.NotEmpty(response.RegistrationId);

        var savedProfile = await dbContext.SellerProfiles.FirstOrDefaultAsync(s => s.UserId == userId);
        Assert.NotNull(savedProfile);
        Assert.Equal("Nexus Official Store", savedProfile.ShopName);
        Assert.Equal("business@nexus.local", savedProfile.BusinessEmail);
        Assert.Equal("+84901234567", savedProfile.Hotline);
        Assert.Equal(SellerProfileStatus.Pending, savedProfile.Status);

        // Verify IdentifyNumber was synced to User
        var updatedUser = await dbContext.Users.FindAsync(userId);
        Assert.Equal("079201001234", updatedUser!.IdentifyNumber);

        // Verify OTP was saved in Redis
        var savedOtp = await otpService.GetOtpAsync("business@nexus.local", purpose: "seller_registration");
        Assert.NotNull(savedOtp);
        Assert.Equal(6, savedOtp.Code.Length);
    }

    [Fact]
    public async Task RegisterSellerAsync_MissingPayload_ThrowsValidationException()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new SellerService(dbContext, new FakeRedisOtpService(), null, NullLogger<SellerService>.Instance);

        var ex = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            service.RegisterSellerAsync(Guid.NewGuid(), null!, CancellationToken.None));

        Assert.Equal("VAL_INVALID_INPUT", ex.ErrorCode);
    }

    [Fact]
    public async Task RegisterSellerAsync_EmptyUserId_ThrowsValidationException()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new SellerService(dbContext, new FakeRedisOtpService(), null, NullLogger<SellerService>.Instance);

        var dto = new CreateSellerRegistrationDto
        {
            ShopName = "Test Shop",
            BusinessEmail = "shop@test.com",
            Hotline = "0901234567",
            IdentityCardNumber = "0123456789"
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
        var service = new SellerService(dbContext, new FakeRedisOtpService(), null, NullLogger<SellerService>.Instance);

        var dto = new CreateSellerRegistrationDto
        {
            ShopName = invalidShopName,
            BusinessEmail = "shop@test.com",
            Hotline = "0901234567",
            IdentityCardNumber = "0123456789"
        };

        var ex = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            service.RegisterSellerAsync(Guid.NewGuid(), dto, CancellationToken.None));

        Assert.Equal("VAL_INVALID_SHOP_NAME", ex.ErrorCode);
    }

    [Fact]
    public async Task RegisterSellerAsync_UserNotFound_ThrowsDomainException()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new SellerService(dbContext, new FakeRedisOtpService(), null, NullLogger<SellerService>.Instance);

        var dto = new CreateSellerRegistrationDto
        {
            ShopName = "Valid Shop",
            BusinessEmail = "shop@test.com",
            Hotline = "0901234567",
            IdentityCardNumber = "0123456789"
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

        var service = new SellerService(dbContext, new FakeRedisOtpService(), null, NullLogger<SellerService>.Instance);
        var dto = new CreateSellerRegistrationDto
        {
            ShopName = "Valid Shop",
            BusinessEmail = "shop@test.com",
            Hotline = "0901234567",
            IdentityCardNumber = "0123456789"
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

        var service = new SellerService(dbContext, new FakeRedisOtpService(), null, NullLogger<SellerService>.Instance);
        var dto = new CreateSellerRegistrationDto
        {
            ShopName = "New Shop Attempt",
            BusinessEmail = "shop@test.com",
            Hotline = "0901234567",
            IdentityCardNumber = "0123456789"
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

        var service = new SellerService(dbContext, new FakeRedisOtpService(), null, NullLogger<SellerService>.Instance);
        var dto = new CreateSellerRegistrationDto
        {
            ShopName = "Another Shop",
            BusinessEmail = "shop@test.com",
            Hotline = "0901234567",
            IdentityCardNumber = "0123456789"
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

        var service = new SellerService(dbContext, new FakeRedisOtpService(), null, NullLogger<SellerService>.Instance);
        var dto = new CreateSellerRegistrationDto
        {
            ShopName = "takenshop", // case-insensitive check
            BusinessEmail = "shop@test.com",
            Hotline = "0901234567",
            IdentityCardNumber = "0123456789"
        };

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.RegisterSellerAsync(userId, dto, CancellationToken.None));

        Assert.Equal("SHOP_NAME_TAKEN", ex.ErrorCode);
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
            CurrentScore = 40 // < 50
        });
        await dbContext.SaveChangesAsync();

        var service = new SellerService(dbContext, new FakeRedisOtpService(), null, NullLogger<SellerService>.Instance);
        var dto = new CreateSellerRegistrationDto
        {
            ShopName = "Low Rep Shop",
            BusinessEmail = "shop@test.com",
            Hotline = "0901234567",
            IdentityCardNumber = "0123456789"
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

        var service = new SellerService(dbContext, new FakeRedisOtpService(), null, NullLogger<SellerService>.Instance);
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

        var response = await service.RegisterSellerAsync(userId, dto, CancellationToken.None);

        Assert.Equal(existingProfileId.ToString(), response.RegistrationId);
        Assert.Equal("PENDING", response.Status);

        var updatedProfile = await dbContext.SellerProfiles.FirstOrDefaultAsync(s => s.UserId == userId);
        Assert.NotNull(updatedProfile);
        Assert.Equal("New Better Shop", updatedProfile.ShopName);
        Assert.Equal(SellerProfileStatus.Pending, updatedProfile.Status);
        Assert.Null(updatedProfile.RejectionReason);
    }

    [Fact]
    public async Task GetSellerRegistrationsAsync_ReturnsPagedItems_WithStatusFilter()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new SellerService(dbContext, new FakeRedisOtpService(), null, NullLogger<SellerService>.Instance);

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

        Assert.Equal(1, result.Total);
        Assert.Single(result.Items);
        Assert.Equal("PENDING", result.Items[0].Status);
        Assert.Equal("Pending Shop", result.Items[0].ShopName);
    }

    [Fact]
    public async Task ApproveSellerRegistrationAsync_PendingProfile_GrantsSellerRole_UpdatesStatus_AndRecordsAuditLog()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new SellerService(dbContext, new FakeRedisOtpService(), null, NullLogger<SellerService>.Instance);

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
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "01",
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
    public async Task ApproveSellerRegistrationAsync_NotFound_ThrowsDomainException()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new SellerService(dbContext, new FakeRedisOtpService(), null, NullLogger<SellerService>.Instance);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.ApproveSellerRegistrationAsync(Guid.NewGuid(), Guid.NewGuid(), null, CancellationToken.None));

        Assert.Equal("REGISTRATION_NOT_FOUND", ex.ErrorCode);
    }

    [Fact]
    public async Task ApproveSellerRegistrationAsync_AlreadyApproved_ThrowsDomainException()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new SellerService(dbContext, new FakeRedisOtpService(), null, NullLogger<SellerService>.Instance);

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
        var service = new SellerService(dbContext, new FakeRedisOtpService(), null, NullLogger<SellerService>.Instance);

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
        var response = await service.RejectSellerRegistrationAsync(regId, staffId, new RejectSellerRegistrationDto
        {
            RejectionReason = "Identity card image is blurry or expired"
        }, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(regId.ToString(), response.RegistrationId);
        Assert.Equal("REJECTED", response.Status);
        Assert.Equal("Identity card image is blurry or expired", response.RejectionReason);

        var updatedProfile = await dbContext.SellerProfiles.FindAsync(regId);
        Assert.Equal(SellerProfileStatus.Rejected, updatedProfile!.Status);
        Assert.Equal("Identity card image is blurry or expired", updatedProfile.RejectionReason);
        Assert.Equal(staffId, updatedProfile.ReviewedBy);

        var audit = await dbContext.AuditLogs.FirstOrDefaultAsync(a => a.EntityId == regId.ToString() && a.Action == AuditAction.SellerReject);
        Assert.NotNull(audit);
    }

    [Fact]
    public async Task RejectSellerRegistrationAsync_MissingReason_ThrowsValidationException()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new SellerService(dbContext, new FakeRedisOtpService(), null, NullLogger<SellerService>.Instance);

        var ex = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            service.RejectSellerRegistrationAsync(Guid.NewGuid(), Guid.NewGuid(), new RejectSellerRegistrationDto { RejectionReason = "" }, CancellationToken.None));

        Assert.Equal("VAL_INVALID_INPUT", ex.ErrorCode);
    }
}
