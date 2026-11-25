namespace User.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using User.Api.Controllers;
using User.Api.Controllers.Internal;
using User.Domain.Exceptions;
using User.Application.Services.Address;
using User.Application.Services.Internal;
using User.Domain.Entities;
using User.Domain.Enums;
using User.Infrastructure.Persistence;
using Xunit;

public sealed class UserAddressTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"user_address_test_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<Guid> SeedActiveUserAsync(AppDbContext db, string emailPrefix = "buyer")
    {
        var userId = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = userId,
            Email = $"{emailPrefix}-{userId:N}@nexus.local",
            PasswordHash = "hash",
            FirstName = "Buyer",
            LastName = "Test",
            Status = UserStatus.Active
        });
        await db.SaveChangesAsync();
        return userId;
    }

    private static CreateAddressRequest MakeRequest(string label = "Home", bool isDefault = false) =>
        new(
            RecipientName: "Nguyen Van A",
            Phone: "0901234567",
            Label: label,
            ProvinceId: 202,
            ProvinceName: "Ho Chi Minh",
            DistrictId: 1442,
            DistrictName: "District 1",
            WardCode: "20101",
            WardName: "Ben Nghe",
            AddressLine: "123 Le Loi",
            IsDefault: isDefault);

    [Fact]
    public async Task CreateAddressAsync_FirstAddress_AutomaticallyBecomesDefault()
    {
        using var db = CreateDbContext();
        var userId = await SeedActiveUserAsync(db);
        var service = new AddressService(db, NullLogger<AddressService>.Instance, new ConfirmingFulfillment());

        var created = await service.CreateAddressAsync(userId, MakeRequest("Home", isDefault: false));

        Assert.True(created.IsDefault);
        Assert.Equal(202, created.ProvinceId);
        Assert.Equal(1442, created.DistrictId);
        Assert.Equal("20101", created.WardCode);
    }

    [Fact]
    public async Task CreateAddressAsync_NewDefault_ClearsPreviousDefault()
    {
        using var db = CreateDbContext();
        var userId = await SeedActiveUserAsync(db);
        var service = new AddressService(db, NullLogger<AddressService>.Instance, new ConfirmingFulfillment());

        var first = await service.CreateAddressAsync(userId, MakeRequest("Home", isDefault: true));
        var second = await service.CreateAddressAsync(userId, MakeRequest("Office", isDefault: true));

        var all = await service.GetAddressesAsync(userId);
        Assert.Equal(2, all.Count);
        Assert.Single(all, a => a.IsDefault);
        Assert.True(all.Single(a => a.Id == second.Id).IsDefault);
        Assert.False(all.Single(a => a.Id == first.Id).IsDefault);
    }

    [Fact]
    public async Task SetDefaultAddressAsync_SwitchesDefaultAtomically()
    {
        using var db = CreateDbContext();
        var userId = await SeedActiveUserAsync(db);
        var service = new AddressService(db, NullLogger<AddressService>.Instance, new ConfirmingFulfillment());

        var first = await service.CreateAddressAsync(userId, MakeRequest("Home", isDefault: true));
        var second = await service.CreateAddressAsync(userId, MakeRequest("Office", isDefault: false));

        var updated = await service.SetDefaultAddressAsync(userId, second.Id);

        Assert.True(updated.IsDefault);
        var defaultAddr = await service.GetDefaultAddressAsync(userId);
        Assert.NotNull(defaultAddr);
        Assert.Equal(second.Id, defaultAddr!.Id);
    }

    [Fact]
    public async Task UpdateAddressAsync_PromotingToDefault_ClearsPreviousDefault()
    {
        using var db = CreateDbContext();
        var userId = await SeedActiveUserAsync(db);
        var service = new AddressService(db, NullLogger<AddressService>.Instance, new ConfirmingFulfillment());

        var first = await service.CreateAddressAsync(userId, MakeRequest("Home", isDefault: true));
        var second = await service.CreateAddressAsync(userId, MakeRequest("Office", isDefault: false));

        var updated = await service.UpdateAddressAsync(userId, second.Id, new UpdateAddressRequest(
            RecipientName: "Nguyen Van B",
            Phone: "0901234568",
            Label: "Office",
            ProvinceId: 202,
            ProvinceName: "Ho Chi Minh",
            DistrictId: 1442,
            DistrictName: "District 1",
            WardCode: "20101",
            WardName: "Ben Nghe",
            AddressLine: "456 Nguyen Hue",
            IsDefault: true));

        Assert.True(updated.IsDefault);
        var all = await service.GetAddressesAsync(userId);
        Assert.Single(all, a => a.IsDefault);
        Assert.False(all.Single(a => a.Id == first.Id).IsDefault);
        Assert.True(all.Single(a => a.Id == second.Id).IsDefault);
    }

    [Fact]
    public async Task DeleteAddressAsync_WhenDeletingDefault_PromotesMostRecentRemaining()
    {
        using var db = CreateDbContext();
        var userId = await SeedActiveUserAsync(db);
        var service = new AddressService(db, NullLogger<AddressService>.Instance, new ConfirmingFulfillment());

        var first = await service.CreateAddressAsync(userId, MakeRequest("Home", isDefault: true));
        var second = await service.CreateAddressAsync(userId, MakeRequest("Office", isDefault: false));

        await service.DeleteAddressAsync(userId, first.Id);

        var remaining = await service.GetAddressesAsync(userId);
        Assert.Single(remaining);
        Assert.Equal(second.Id, remaining[0].Id);
        Assert.True(remaining[0].IsDefault);

        var deletedEntity = await db.UserAddresses.IgnoreQueryFilters().SingleAsync(a => a.Id == first.Id);
        Assert.True(deletedEntity.IsDeleted);
        Assert.NotNull(deletedEntity.DeletedAt);
    }

    [Fact]
    public async Task UpdateAndDeleteAddressAsync_OtherUsersAddress_ThrowsDomainException()
    {
        using var db = CreateDbContext();
        var userA = await SeedActiveUserAsync(db, "usera");
        var userB = await SeedActiveUserAsync(db, "userb");
        var service = new AddressService(db, NullLogger<AddressService>.Instance, new ConfirmingFulfillment());

        var addrA = await service.CreateAddressAsync(userA, MakeRequest());

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.UpdateAddressAsync(userB, addrA.Id, new UpdateAddressRequest(
                RecipientName: "Hacker",
                Phone: "0900000000",
                Label: "X",
                ProvinceId: 202,
                ProvinceName: "HCM",
                DistrictId: 1442,
                DistrictName: "Q1",
                WardCode: "20101",
                WardName: "BN",
                AddressLine: "999",
                IsDefault: true)));

        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task MeController_AddressEndpoints_WorkEndToEnd()
    {
        using var db = CreateDbContext();
        var userId = await SeedActiveUserAsync(db);
        var addressService = new AddressService(db, NullLogger<AddressService>.Instance, new ConfirmingFulfillment());

        var controller = new MeController(
            profileService: null!,
            logger: NullLogger<MeController>.Instance,
            addressService: addressService);

        var http = new DefaultHttpContext();
        http.Request.Headers["x-user-id"] = userId.ToString();
        http.Request.Headers["x-user-roles"] = "buyer";
        http.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("sub", userId.ToString()),
            new Claim(ClaimTypes.Role, "buyer")
        }, "Test"));
        controller.ControllerContext = new ControllerContext { HttpContext = http };

        var createRes = await controller.CreateMyAddress(MakeRequest("Home", true), CancellationToken.None);
        var createdObj = Assert.IsType<ObjectResult>(createRes);
        Assert.Equal(StatusCodes.Status201Created, createdObj.StatusCode);
        var createdDto = Assert.IsType<UserAddressDto>(createdObj.Value);

        var listRes = await controller.GetMyAddresses(CancellationToken.None);
        var listOk = Assert.IsType<OkObjectResult>(listRes);
        var list = Assert.IsAssignableFrom<IReadOnlyList<UserAddressDto>>(listOk.Value);
        Assert.Single(list);

        var defRes = await controller.GetMyDefaultAddress(CancellationToken.None);
        var defOk = Assert.IsType<OkObjectResult>(defRes);
        Assert.Equal(createdDto.Id, Assert.IsType<UserAddressDto>(defOk.Value).Id);

        var delRes = await controller.DeleteMyAddress(createdDto.Id, CancellationToken.None);
        Assert.IsType<NoContentResult>(delRes);
    }

    [Fact]
    public async Task InternalUsersController_AddressEndpoints_EnforceKeyOwnershipAndReturn200()
    {
        using var db = CreateDbContext();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var userA = await SeedActiveUserAsync(db, "a");
        var userB = await SeedActiveUserAsync(db, "b");
        var addressService = new AddressService(db, NullLogger<AddressService>.Instance, new ConfirmingFulfillment());
        var addrA = await addressService.CreateAddressAsync(userA, MakeRequest("Home", true));

        var internalService = new InternalUserService(db, cache, NullLogger<InternalUserService>.Instance);
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["InternalSecurity:ServiceKey"] = "sec" })
            .Build();

        var controller = new InternalUsersController(null, NullLogger<InternalUsersController>.Instance, internalService);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        // Unauthorized without key
        Assert.IsType<UnauthorizedResult>(await controller.GetUserDefaultAddress(userA, cfg, CancellationToken.None));

        controller.ControllerContext.HttpContext.Request.Headers["X-Internal-Service-Key"] = "sec";

        // 200 for userA default & specific
        var defRes = Assert.IsType<OkObjectResult>(await controller.GetUserDefaultAddress(userA, cfg, CancellationToken.None));
        Assert.NotNull(defRes.Value);
        var specificRes = Assert.IsType<OkObjectResult>(await controller.GetUserAddressById(userA, addrA.Id, cfg, CancellationToken.None));
        Assert.NotNull(specificRes.Value);

        // 404 when userB requests userA's addressId
        Assert.IsType<NotFoundResult>(await controller.GetUserAddressById(userB, addrA.Id, cfg, CancellationToken.None));
        // 404 when userB has no default
        Assert.IsType<NotFoundResult>(await controller.GetUserDefaultAddress(userB, cfg, CancellationToken.None));
    }

    [Fact]
    public void MeController_UpdateMyAddress_HasSingleHttpMethodAttribute()
    {
        var method = typeof(MeController).GetMethod(nameof(MeController.UpdateMyAddress))!;
        var attrs = method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute), inherit: false);
        Assert.Single(attrs);
    }
}
