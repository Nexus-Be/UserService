namespace User.Tests;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using User.Api.Controllers.Internal;
using User.Application.Services.Internal;
using User.Domain.Entities;
using User.Domain.Enums;
using User.Infrastructure.Persistence;
using Xunit;

public sealed class InternalShopAndContactTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"internal_shop_contact_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static IConfiguration BuildConfig(string? secret = "test-internal-secret")
    {
        var dict = new Dictionary<string, string?>();
        if (secret is not null)
        {
            dict["InternalSecurity:ServiceKey"] = secret;
        }
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    private static InternalUsersController CreateController(IInternalUserService service, string? headerKey)
    {
        var controller = new InternalUsersController(
            penaltyService: null,
            logger: NullLogger<InternalUsersController>.Instance,
            internalUserService: service);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        if (headerKey is not null)
        {
            controller.ControllerContext.HttpContext.Request.Headers["X-Internal-Service-Key"] = headerKey;
        }
        return controller;
    }

    [Fact]
    public async Task GetShopAsync_ReturnsShopAndWarehouseDetails_WhenSellerExists()
    {
        using var db = CreateDbContext();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var userId = Guid.NewGuid();
        var shopId = Guid.NewGuid();

        db.SellerProfiles.Add(new SellerProfile
        {
            Id = shopId,
            UserId = userId,
            ShopName = "Nexus Tech Store",
            PickupAddress = "123 Le Loi",
            ProvinceId = 202,
            DistrictId = 1442,
            WardCode = "20101",
            Hotline = "0901234567",
            Status = SellerProfileStatus.Approved
        });
        await db.SaveChangesAsync();

        var service = new InternalUserService(db, cache, NullLogger<InternalUserService>.Instance);
        var shop = await service.GetShopAsync(userId, CancellationToken.None);

        Assert.NotNull(shop);
        Assert.Equal(shopId, shop.ShopId);
        Assert.Equal(userId, shop.UserId);
        Assert.Equal("Nexus Tech Store", shop.ShopName);
        Assert.Equal("Approved", shop.Status);
        Assert.Equal("123 Le Loi", shop.PickupAddress);
        Assert.Equal(202, shop.ProvinceId);
        Assert.Equal(1442, shop.DistrictId);
        Assert.Equal("20101", shop.WardCode);
        Assert.Equal("0901234567", shop.Hotline);
    }

    [Fact]
    public async Task GetShopAsync_ReturnsNull_WhenSellerMissing()
    {
        using var db = CreateDbContext();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new InternalUserService(db, cache, NullLogger<InternalUserService>.Instance);

        var shop = await service.GetShopAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(shop);
    }

    [Fact]
    public async Task GetContactAsync_ReturnsEmailAndFullName_WhenUserExists()
    {
        using var db = CreateDbContext();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var userId = Guid.NewGuid();

        db.Users.Add(new User
        {
            Id = userId,
            Email = "buyer@nexus.local",
            PasswordHash = "hash",
            FirstName = "Nguyen",
            LastName = "An",
            Status = UserStatus.Active
        });
        await db.SaveChangesAsync();

        var service = new InternalUserService(db, cache, NullLogger<InternalUserService>.Instance);
        var contact = await service.GetContactAsync(userId, CancellationToken.None);

        Assert.NotNull(contact);
        Assert.Equal(userId, contact.UserId);
        Assert.Equal("buyer@nexus.local", contact.Email);
        Assert.Equal("Nguyen An", contact.FullName);
    }

    [Fact]
    public async Task GetShop_Controller_EnforcesServiceKey_AndReturns404Or200()
    {
        using var db = CreateDbContext();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var userId = Guid.NewGuid();
        db.SellerProfiles.Add(new SellerProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ShopName = "Shop A",
            PickupAddress = "10 Street",
            ProvinceId = 1,
            DistrictId = 2,
            WardCode = "W1",
            Hotline = "0900000000",
            Status = SellerProfileStatus.Approved
        });
        await db.SaveChangesAsync();

        var service = new InternalUserService(db, cache, NullLogger<InternalUserService>.Instance);

        // Unauthorized with wrong key
        var unauthCtrl = CreateController(service, "wrong-key");
        var unauthRes = await unauthCtrl.GetShop(userId, BuildConfig(), CancellationToken.None);
        Assert.IsType<UnauthorizedResult>(unauthRes);

        // 503 when secret not configured
        var noCfgCtrl = CreateController(service, "any");
        var noCfgRes = await noCfgCtrl.GetShop(userId, BuildConfig(secret: null), CancellationToken.None);
        var statusRes = Assert.IsType<StatusCodeResult>(noCfgRes);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, statusRes.StatusCode);

        // 404 when user has no shop
        var okCtrl = CreateController(service, "test-internal-secret");
        var notFoundRes = await okCtrl.GetShop(Guid.NewGuid(), BuildConfig(), CancellationToken.None);
        Assert.IsType<NotFoundResult>(notFoundRes);

        // 200 when valid (pure DTO, no manual { data = ... } wrapper)
        var okRes = Assert.IsType<OkObjectResult>(await okCtrl.GetShop(userId, BuildConfig(), CancellationToken.None));
        Assert.IsType<InternalShopResponse>(okRes.Value);
    }

    [Fact]
    public async Task GetContact_Controller_Returns200_WhenValid()
    {
        using var db = CreateDbContext();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var userId = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = userId,
            Email = "contact@nexus.local",
            PasswordHash = "hash",
            FirstName = "Tran",
            LastName = "Binh",
            Status = UserStatus.Active
        });
        await db.SaveChangesAsync();

        var service = new InternalUserService(db, cache, NullLogger<InternalUserService>.Instance);
        var ctrl = CreateController(service, "test-internal-secret");

        var res = Assert.IsType<OkObjectResult>(await ctrl.GetContact(new InternalContactRequest(userId), BuildConfig(), CancellationToken.None));
        Assert.IsType<InternalContactResponse>(res.Value);
    }
}
