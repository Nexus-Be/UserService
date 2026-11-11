namespace User.Tests;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using User.Application.Interfaces;
using User.Application.Models;
using User.Application.Services.Seller;
using User.Domain.Entities;
using User.Domain.Enums;
using User.Infrastructure.Persistence;
using Xunit;

public sealed class SellerServiceFulfillmentSyncTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"seller_sync_test_{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    private sealed class TrackingFulfillmentServiceClient : IFulfillmentServiceClient
    {
        public Guid? LastDeletedSellerId { get; private set; }
        public Guid? LastUpdatedSellerId { get; private set; }

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
            return Task.FromResult(true);
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
            LastUpdatedSellerId = sellerId;
            return Task.FromResult(true);
        }

        public Task<bool> DeleteWarehouseBySellerIdAsync(Guid sellerId, CancellationToken cancellationToken = default)
        {
            LastDeletedSellerId = sellerId;
            return Task.FromResult(true);
        }
    }

    private sealed class DummyRedisOtpService : IRedisOtpService
    {
        public Task SaveOtpAsync(string email, string code, TimeSpan validityDuration, TimeSpan retentionDuration, string purpose = "registration") => Task.CompletedTask;
        public Task<OtpEntry?> GetOtpAsync(string email, string purpose = "registration") => Task.FromResult<OtpEntry?>(null);
        public Task MarkOtpConsumedAsync(string email, string purpose = "registration") => Task.CompletedTask;
        public Task InvalidateExistingOtpsAsync(string email, string purpose = "registration") => Task.CompletedTask;
        public Task<int> IncrementLoginAttemptsAsync(string identifier, TimeSpan window) => Task.FromResult(0);
        public Task<bool> IsAccountLockedAsync(string identifier) => Task.FromResult(false);
        public Task LockAccountAsync(string identifier, TimeSpan duration) => Task.CompletedTask;
        public Task ResetLoginAttemptsAsync(string identifier) => Task.CompletedTask;
    }

    [Fact]
    public async Task RejectSellerRegistrationAsync_calls_DeleteWarehouseBySellerIdAsync_with_sellerProfile_UserId()
    {
        using var dbContext = CreateInMemoryDbContext();
        var fakeFulfillment = new TrackingFulfillmentServiceClient();
        var service = new SellerService(
            dbContext,
            new DummyRedisOtpService(),
            null,
            fakeFulfillment,
            NullLogger<SellerService>.Instance);

        var userId = Guid.NewGuid();
        var regId = Guid.NewGuid();
        dbContext.SellerProfiles.Add(new SellerProfile
        {
            Id = regId,
            UserId = userId,
            ShopName = "Reject Seller Shop",
            Hotline = "0901234567",
            PickupAddress = "Address 123",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "01",
            Status = SellerProfileStatus.Pending
        });
        await dbContext.SaveChangesAsync();

        var staffId = Guid.NewGuid();
        await service.RejectSellerRegistrationAsync(
            regId,
            staffId,
            new RejectSellerRegistrationDto { RejectionReason = "Incomplete business profile" },
            CancellationToken.None);

        // Under standardized 1 Seller - 1 Warehouse principle, warehouse is keyed by User.Id
        Assert.Equal(userId, fakeFulfillment.LastDeletedSellerId);
    }

    [Fact]
    public async Task UpdateSellerOwnProfileAsync_calls_UpdateWarehouseAsync_with_sellerProfile_UserId()
    {
        using var dbContext = CreateInMemoryDbContext();
        var fakeFulfillment = new TrackingFulfillmentServiceClient();
        var service = new SellerService(
            dbContext,
            new DummyRedisOtpService(),
            null,
            fakeFulfillment,
            NullLogger<SellerService>.Instance);

        var userId = Guid.NewGuid();
        dbContext.Users.Add(new User
        {
            Id = userId,
            Email = "seller@nexus.local",
            PasswordHash = "hash",
            FirstName = "Van",
            LastName = "A",
            Status = UserStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var profileId = Guid.NewGuid();
        dbContext.SellerProfiles.Add(new SellerProfile
        {
            Id = profileId,
            UserId = userId,
            ShopName = "Original Shop",
            Hotline = "0901234567",
            PickupAddress = "Old Address",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "01",
            Status = SellerProfileStatus.Approved
        });
        await dbContext.SaveChangesAsync();

        var updateDto = new UpdateSellerOwnProfileDto
        {
            ShopName = "Original Shop",
            Hotline = "0909999999",
            PickupAddress = "New Address 456",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "01"
        };

        await service.UpdateSellerOwnProfileAsync(userId, updateDto, CancellationToken.None);

        // Under standardized 1 Seller - 1 Warehouse principle, warehouse is keyed by User.Id
        Assert.Equal(userId, fakeFulfillment.LastUpdatedSellerId);
    }
}
