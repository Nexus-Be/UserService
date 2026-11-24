namespace User.Tests;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using User.Application.Interfaces;
using User.Application.Services.Address;
using User.Application.Services.Seller;
using User.Domain.Entities;
using User.Domain.Enums;
using User.Domain.Exceptions;
using User.Infrastructure.Persistence;
using Xunit;

/// <summary>
/// An address (buyer address book, seller pickup address) is saved only as a province, district and ward chain that the GHN lists confirm.
/// A chain the lists reject is 400 VAL_INVALID_LOCATION; with no trusted GHN source it is 503 LOCATION_SOURCE_UNAVAILABLE; in both nothing
/// changes. A change that does not touch the chain is never held up by the lists.
/// </summary>
public sealed class LocationVerificationTests
{
    private static AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"loc_{Guid.NewGuid():N}").Options);

    private static async Task<Guid> SeedUserAsync(AppDbContext db)
    {
        var id = Guid.NewGuid();
        db.Users.Add(new User { Id = id, Email = $"u{id:N}@test.com", FirstName = "A", LastName = "B", PasswordHash = "h", Status = UserStatus.Active });
        await db.SaveChangesAsync();
        return id;
    }

    private static CreateAddressRequest Create(int province = 202, int district = 1442, string ward = "20101") => new(
        RecipientName: "QA", Phone: "0912345678", Label: "Home", ProvinceId: province, ProvinceName: "Ho Chi Minh",
        DistrictId: district, DistrictName: "Quan 1", WardCode: ward, WardName: "Ben Nghe", AddressLine: "1 Le Loi", IsDefault: false);

    private static UpdateAddressRequest Update(string recipient = "QA", int province = 202, int district = 1442, string ward = "20101") => new(
        RecipientName: recipient, Phone: "0912345678", Label: "Home", ProvinceId: province, ProvinceName: "Ho Chi Minh",
        DistrictId: district, DistrictName: "Quan 1", WardCode: ward, WardName: "Ben Nghe", AddressLine: "1 Le Loi", IsDefault: false);

    [Fact]
    public async Task A_new_address_is_saved_after_the_chain_is_confirmed()
    {
        using var db = NewDb();
        var userId = await SeedUserAsync(db);
        var fulfillment = new ConfirmingFulfillment();

        await new AddressService(db, NullLogger<AddressService>.Instance, fulfillment).CreateAddressAsync(userId, Create());

        Assert.Single(fulfillment.Asked);
        Assert.Equal((202, 1442, "20101"), fulfillment.Asked[0]);
        Assert.Equal(1, await db.UserAddresses.CountAsync());
    }

    [Fact]
    public async Task A_chain_the_lists_reject_is_400_and_nothing_is_saved()
    {
        using var db = NewDb();
        var userId = await SeedUserAsync(db);
        var fulfillment = new ConfirmingFulfillment { Answer = LocationCheck.Rejected("The ward does not belong to the district.") };

        var ex = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            new AddressService(db, NullLogger<AddressService>.Instance, fulfillment).CreateAddressAsync(userId, Create(ward: "144201")));

        Assert.Equal("VAL_INVALID_LOCATION", ex.ErrorCode);
        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(0, await db.UserAddresses.CountAsync());
    }

    [Fact]
    public async Task With_no_trusted_GHN_source_the_address_is_503_and_nothing_is_saved()
    {
        using var db = NewDb();
        var userId = await SeedUserAsync(db);
        var fulfillment = new ConfirmingFulfillment { Answer = LocationCheck.SourceUnavailable() };

        var ex = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            new AddressService(db, NullLogger<AddressService>.Instance, fulfillment).CreateAddressAsync(userId, Create()));

        Assert.Equal("LOCATION_SOURCE_UNAVAILABLE", ex.ErrorCode);
        Assert.Equal(503, ex.StatusCode);
        Assert.Equal(0, await db.UserAddresses.CountAsync());
    }

    [Fact]
    public async Task Changing_only_the_recipient_does_not_ask_the_lists_but_changing_the_ward_does()
    {
        using var db = NewDb();
        var userId = await SeedUserAsync(db);
        var fulfillment = new ConfirmingFulfillment();
        var service = new AddressService(db, NullLogger<AddressService>.Instance, fulfillment);
        var created = await service.CreateAddressAsync(userId, Create());
        fulfillment.Asked.Clear();

        // the source is down, yet a new recipient name is saved: the chain did not change
        fulfillment.Answer = LocationCheck.SourceUnavailable();
        var renamed = await service.UpdateAddressAsync(userId, created.Id, Update(recipient: "New name"));
        Assert.Equal("New name", renamed.RecipientName);
        Assert.Empty(fulfillment.Asked);

        // a new ward with the source down: 503 and the old ward stays
        var ex = await Assert.ThrowsAsync<BusinessRuleValidationException>(() => service.UpdateAddressAsync(userId, created.Id, Update(ward: "20102")));
        Assert.Equal(503, ex.StatusCode);
        Assert.Equal("20101", (await db.UserAddresses.SingleAsync()).WardCode);

        // the source is back: the new ward is checked and saved
        fulfillment.Answer = LocationCheck.Confirmed();
        await service.UpdateAddressAsync(userId, created.Id, Update(ward: "20102"));
        Assert.Equal((202, 1442, "20102"), fulfillment.Asked[^1]);   // the refused attempt asked once too
        Assert.Equal("20102", (await db.UserAddresses.SingleAsync()).WardCode);
    }

    // ---- the seller profile
    private static async Task<(AppDbContext Db, SellerService Service, ConfirmingFulfillment Fulfillment, Guid UserId, Guid ProfileId)> SellerAsync()
    {
        var db = NewDb();
        var fulfillment = new ConfirmingFulfillment();
        var userId = await SeedUserAsync(db);
        var profileId = Guid.NewGuid();
        db.SellerProfiles.Add(new SellerProfile
        {
            Id = profileId, UserId = userId, ShopName = "Shop", BusinessEmail = "s@shop.com", Hotline = "0901111111",
            PickupAddress = "1 Le Loi", ProvinceId = 202, DistrictId = 1442, WardCode = "20101", Status = SellerProfileStatus.Approved,
        });
        await db.SaveChangesAsync();
        var service = SellerServiceTests.CreateSellerService(db, fulfillment: fulfillment);
        return (db, service, fulfillment, userId, profileId);
    }

    [Fact]
    public async Task A_changed_pickup_chain_is_saved_only_when_the_lists_confirm_it()
    {
        var (db, service, fulfillment, userId, profileId) = await SellerAsync();
        using var _ = db;

        await service.UpdateSellerOwnProfileAsync(userId, new UpdateSellerOwnProfileDto { WardCode = "20102" }, CancellationToken.None);

        Assert.Equal((202, 1442, "20102"), fulfillment.Asked.Single());
        Assert.Equal("20102", (await db.SellerProfiles.FindAsync(profileId))!.WardCode);
    }

    [Fact]
    public async Task A_rejected_or_unconfirmable_pickup_chain_changes_nothing()
    {
        var (db, service, fulfillment, userId, profileId) = await SellerAsync();
        using var _ = db;

        fulfillment.Answer = LocationCheck.Rejected("no");
        var rejected = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            service.UpdateSellerOwnProfileAsync(userId, new UpdateSellerOwnProfileDto { ProvinceId = 201, DistrictId = 1442, WardCode = "20101" }, CancellationToken.None));
        Assert.Equal("VAL_INVALID_LOCATION", rejected.ErrorCode);

        fulfillment.Answer = LocationCheck.SourceUnavailable();
        var unavailable = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            service.UpdateSellerOwnProfileAsync(userId, new UpdateSellerOwnProfileDto { DistrictId = 1443 }, CancellationToken.None));
        Assert.Equal(503, unavailable.StatusCode);

        var profile = (await db.SellerProfiles.FindAsync(profileId))!;
        Assert.Equal((202, 1442, "20101"), (profile.ProvinceId, profile.DistrictId, profile.WardCode));
    }

    [Fact]
    public async Task A_change_of_hotline_or_street_alone_is_not_held_up_by_the_lists()
    {
        var (db, service, fulfillment, userId, profileId) = await SellerAsync();
        using var _ = db;
        fulfillment.Answer = LocationCheck.SourceUnavailable();

        await service.UpdateSellerOwnProfileAsync(userId, new UpdateSellerOwnProfileDto { Hotline = "0903333333", PickupAddress = "2 Le Loi" }, CancellationToken.None);

        Assert.Empty(fulfillment.Asked);
        var profile = (await db.SellerProfiles.FindAsync(profileId))!;
        Assert.Equal("0903333333", profile.Hotline);
        Assert.Equal("2 Le Loi", profile.PickupAddress);
        // the same codes sent again are not a change either
        await service.UpdateSellerOwnProfileAsync(userId, new UpdateSellerOwnProfileDto { ProvinceId = 202, DistrictId = 1442, WardCode = "20101" }, CancellationToken.None);
        Assert.Empty(fulfillment.Asked);
    }
}
