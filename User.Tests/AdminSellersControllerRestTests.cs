namespace User.Tests;

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using User.Api.Controllers;
using User.Application.Interfaces;
using User.Application.Models;
using User.Application.Services.Seller;
using User.Domain.Entities;
using User.Domain.Enums;
using User.Infrastructure.Persistence;
using Xunit;

public sealed class AdminSellersControllerRestTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"admin_sellers_test_db_{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    private sealed class NoOpFulfillmentServiceClient : IFulfillmentServiceClient
    {
        public Task<bool> CreateWarehouseAsync(Guid sellerId, Guid userId, string shopName, string contactName, string contactPhone, string pickupAddress, int provinceId, int districtId, string wardCode, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> UpdateWarehouseAsync(Guid sellerId, string shopName, string contactPhone, string pickupAddress, int provinceId, int districtId, string wardCode, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> DeleteWarehouseBySellerIdAsync(Guid sellerId, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class NoOpRedisOtpService : IRedisOtpService
    {
        public readonly ConcurrentDictionary<string, string> Drafts = new(StringComparer.OrdinalIgnoreCase);
        public readonly ConcurrentDictionary<string, OtpEntry> Otps = new(StringComparer.OrdinalIgnoreCase);

        public Task SaveOtpAsync(string email, string code, TimeSpan validityDuration, TimeSpan retentionDuration, string purpose = "registration")
        {
            Otps[$"{purpose}:{email}"] = new OtpEntry
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
            Otps.TryGetValue($"{purpose}:{email}", out var entry);
            return Task.FromResult(entry);
        }

        public Task MarkOtpConsumedAsync(string email, string purpose = "registration")
        {
            if (Otps.TryGetValue($"{purpose}:{email}", out var entry))
            {
                entry.Status = OtpStatus.Consumed;
            }
            return Task.CompletedTask;
        }

        public Task InvalidateExistingOtpsAsync(string email, string purpose = "registration")
        {
            Otps.TryRemove($"{purpose}:{email}", out _);
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

    private static SellerService CreateSellerService(AppDbContext dbContext) =>
        new(dbContext, new NoOpRedisOtpService(), null, new NoOpFulfillmentServiceClient(), NullLogger<SellerService>.Instance);

    [Fact]
    public async Task GetAdminSellerProfile_WhenSellerExists_Returns200OkWithProfile()
    {
        using var dbContext = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var user = new Domain.Entities.User
        {
            Id = userId,
            Email = "admin_seller_test@nexus.local",
            PasswordHash = "hash",
            FirstName = "Tran",
            LastName = "Van B",
            PhoneNumber = "0912345678",
            IdentifyNumber = "123456789012",
            Status = UserStatus.Active,
            ReputationScore = new ReputationScore
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CurrentScore = 80,
                TrustLevel = TrustLevel.Good
            }
        };
        dbContext.Users.Add(user);

        var sellerId = Guid.NewGuid();
        var profile = new SellerProfile
        {
            Id = sellerId,
            UserId = userId,
            ShopName = "Nexus Official Store",
            Hotline = "0987654321",
            PickupAddress = "456 Le Loi, District 1, HCMC",
            ProvinceId = 202,
            DistrictId = 1442,
            WardCode = "20101",
            Status = SellerProfileStatus.Approved
        };
        dbContext.SellerProfiles.Add(profile);
        await dbContext.SaveChangesAsync();

        var sellerService = CreateSellerService(dbContext);
        var controller = new AdminSellersController(sellerService, NullLogger<AdminSellersController>.Instance);

        var result = await controller.GetAdminSellerProfile(sellerId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AdminSellerProfileResponseDto>(okResult.Value);
        Assert.Equal(sellerId.ToString(), response.SellerId);
        Assert.Equal(userId.ToString(), response.UserId);
        Assert.Equal("Nexus Official Store", response.ShopName);
        Assert.Equal("Tran Van B", response.OwnerFullName);
        Assert.Equal(80, response.ReputationScore);
        Assert.Equal("Good", response.TrustLevel);
    }
}
