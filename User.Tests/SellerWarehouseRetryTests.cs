namespace User.Tests;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using User.Application.Interfaces;
using User.Application.Services.Seller;
using User.Domain.Entities;
using User.Domain.Enums;
using User.Domain.Exceptions;
using User.Infrastructure.Persistence;
using Xunit;

/// <summary>
/// A seller approved while Fulfillment was down has no warehouse and cannot ship. The approval still goes through, but the seller is marked
/// (WarehouseReady = false), retried automatically and listed for admins.
/// </summary>
public sealed class SellerWarehouseRetryTests
{
    private sealed class ScriptedFulfillment : IFulfillmentServiceClient
    {
        public bool Succeed { get; set; }
        public int Calls { get; private set; }

        public Task<bool> CreateWarehouseAsync(Guid sellerId, Guid userId, string shopName, string contactName, string contactPhone, string pickupAddress, int provinceId, int districtId, string wardCode, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(Succeed);
        }

        public Task<bool> UpdateWarehouseAsync(Guid sellerId, string shopName, string contactPhone, string pickupAddress, int provinceId, int districtId, string wardCode, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<bool> DeleteWarehouseBySellerIdAsync(Guid sellerId, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"seller_wh_{Guid.NewGuid():N}").Options);

    private static SellerService NewService(AppDbContext db, ScriptedFulfillment fulfillment) =>
        new(db, null!, null, fulfillment, NullLogger<SellerService>.Instance);

    private static async Task<SellerProfile> AddSellerAsync(AppDbContext db, SellerProfileStatus status, bool warehouseReady)
    {
        var userId = Guid.NewGuid();
        db.Users.Add(new User { Id = userId, Email = $"{userId:N}@nexus.local", FirstName = "Van", LastName = "B", PasswordHash = "h", Status = UserStatus.Active });
        var profile = new SellerProfile
        {
            Id = Guid.NewGuid(), UserId = userId, ShopName = "Shop " + userId.ToString("N")[..4], Hotline = "0901234567", PickupAddress = "1 Le Loi",
            ProvinceId = 202, DistrictId = 1442, WardCode = "20101", Status = status, WarehouseReady = warehouseReady
        };
        db.SellerProfiles.Add(profile);
        await db.SaveChangesAsync();
        return profile;
    }

    [Fact]
    public async Task Approval_WhenTheWarehouseCannotBeCreated_StillApproves_ButMarksTheSeller()
    {
        using var db = NewDb();
        db.Roles.Add(new Role { Id = Guid.NewGuid(), RoleCode = RoleType.Seller, RoleName = "Seller" });
        var profile = await AddSellerAsync(db, SellerProfileStatus.Pending, warehouseReady: true);
        var service = NewService(db, new ScriptedFulfillment { Succeed = false });

        await service.ApproveSellerRegistrationAsync(profile.Id, Guid.NewGuid(), new ApproveSellerRegistrationDto { Note = "ok" }, CancellationToken.None);

        var stored = await db.SellerProfiles.AsNoTracking().SingleAsync();
        Assert.Equal(SellerProfileStatus.Approved, stored.Status);
        Assert.False(stored.WarehouseReady);
    }

    [Fact]
    public async Task Approval_WhenTheWarehouseIsCreated_LeavesTheSellerReady()
    {
        using var db = NewDb();
        db.Roles.Add(new Role { Id = Guid.NewGuid(), RoleCode = RoleType.Seller, RoleName = "Seller" });
        var profile = await AddSellerAsync(db, SellerProfileStatus.Pending, warehouseReady: false);
        var service = NewService(db, new ScriptedFulfillment { Succeed = true });

        await service.ApproveSellerRegistrationAsync(profile.Id, Guid.NewGuid(), new ApproveSellerRegistrationDto { Note = "ok" }, CancellationToken.None);

        Assert.True((await db.SellerProfiles.AsNoTracking().SingleAsync()).WarehouseReady);
    }

    [Fact]
    public async Task Retry_KeepsTheSellerMarkedWhileFulfillmentFails_ThenClearsItOnceItWorks()
    {
        using var db = NewDb();
        var missing = await AddSellerAsync(db, SellerProfileStatus.Approved, warehouseReady: false);
        var ready = await AddSellerAsync(db, SellerProfileStatus.Approved, warehouseReady: true);
        var fulfillment = new ScriptedFulfillment { Succeed = false };
        var service = NewService(db, fulfillment);

        var first = await service.RetryMissingWarehousesAsync(20);
        Assert.Equal(0, first);
        Assert.False((await db.SellerProfiles.AsNoTracking().SingleAsync(s => s.Id == missing.Id)).WarehouseReady);

        fulfillment.Succeed = true;
        var second = await service.RetryMissingWarehousesAsync(20);

        Assert.Equal(1, second);
        Assert.True((await db.SellerProfiles.AsNoTracking().SingleAsync(s => s.Id == missing.Id)).WarehouseReady);
        Assert.Equal(2, fulfillment.Calls); // the seller that was already ready is never sent again
        Assert.True((await db.SellerProfiles.AsNoTracking().SingleAsync(s => s.Id == ready.Id)).WarehouseReady);
    }

    [Fact]
    public async Task Retry_IgnoresSellersWhoAreNotApproved()
    {
        using var db = NewDb();
        await AddSellerAsync(db, SellerProfileStatus.Pending, warehouseReady: false);
        var fulfillment = new ScriptedFulfillment { Succeed = true };

        var count = await NewService(db, fulfillment).RetryMissingWarehousesAsync(20);

        Assert.Equal(0, count);
        Assert.Equal(0, fulfillment.Calls);
    }

    [Fact]
    public async Task AdminRetry_CreatesTheWarehouse_AndReportsReady()
    {
        using var db = NewDb();
        var missing = await AddSellerAsync(db, SellerProfileStatus.Approved, warehouseReady: false);
        var service = NewService(db, new ScriptedFulfillment { Succeed = true });

        var ready = await service.RetryWarehouseAsync(missing.Id);

        Assert.True(ready);
        Assert.True((await db.SellerProfiles.AsNoTracking().SingleAsync()).WarehouseReady);
    }

    [Fact]
    public async Task AdminRetry_WhileFulfillmentStillFails_ReportsNotReady()
    {
        using var db = NewDb();
        var missing = await AddSellerAsync(db, SellerProfileStatus.Approved, warehouseReady: false);

        var ready = await NewService(db, new ScriptedFulfillment { Succeed = false }).RetryWarehouseAsync(missing.Id);

        Assert.False(ready);
        Assert.False((await db.SellerProfiles.AsNoTracking().SingleAsync()).WarehouseReady);
    }

    [Fact]
    public async Task AdminRetry_ForASellerWhoIsNotApproved_IsRefused()
    {
        using var db = NewDb();
        var pending = await AddSellerAsync(db, SellerProfileStatus.Pending, warehouseReady: false);

        var ex = await Assert.ThrowsAsync<DomainException>(() => NewService(db, new ScriptedFulfillment { Succeed = true }).RetryWarehouseAsync(pending.Id));

        Assert.Equal("SELLER_NOT_APPROVED", ex.ErrorCode);
    }

    [Fact]
    public async Task AdminRetry_ForAnUnknownSeller_IsNotFound()
    {
        using var db = NewDb();

        var ex = await Assert.ThrowsAsync<DomainException>(() => NewService(db, new ScriptedFulfillment()).RetryWarehouseAsync(Guid.NewGuid()));

        Assert.Equal("SELLER_NOT_FOUND", ex.ErrorCode);
    }

    [Fact]
    public async Task AdminList_CanBeFilteredToTheSellersMissingAWarehouse()
    {
        using var db = NewDb();
        var missing = await AddSellerAsync(db, SellerProfileStatus.Approved, warehouseReady: false);
        await AddSellerAsync(db, SellerProfileStatus.Approved, warehouseReady: true);
        var service = NewService(db, new ScriptedFulfillment());

        var onlyMissing = await service.GetAdminSellersAsync(1, 20, null, null, CancellationToken.None, warehouseReady: false);
        var all = await service.GetAdminSellersAsync(1, 20, null, null, CancellationToken.None);

        Assert.Single(onlyMissing.Items);
        Assert.Equal(missing.Id, onlyMissing.Items[0].SellerId);
        Assert.False(onlyMissing.Items[0].WarehouseReady);
        Assert.Equal(2, all.Items.Count);
    }
}
