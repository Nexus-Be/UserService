namespace User.Tests;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using User.Api.Controllers;
using User.Application.Interfaces;
using User.Application.Models;
using User.Application.Services.Seller;
using User.Domain.Entities;
using User.Domain.Enums;
using User.Application.Common.Models;
using User.Infrastructure.Persistence;
using Xunit;

public sealed class SellerControllerRestTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"seller_controller_test_db_{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    private sealed class NoOpRedisOtpService : IRedisOtpService
    {
        public Task SaveOtpAsync(string email, string code, TimeSpan validityDuration, TimeSpan retentionDuration, string purpose = "registration") => Task.CompletedTask;
        public Task<OtpEntry?> GetOtpAsync(string email, string purpose = "registration") => Task.FromResult<OtpEntry?>(null);
        public Task MarkOtpConsumedAsync(string email, string purpose = "registration") => Task.CompletedTask;
        public Task InvalidateExistingOtpsAsync(string email, string purpose = "registration") => Task.CompletedTask;
        public Task<int> IncrementLoginAttemptsAsync(string identifier, TimeSpan window) => Task.FromResult(1);
        public Task<bool> IsAccountLockedAsync(string identifier) => Task.FromResult(false);
        public Task LockAccountAsync(string identifier, TimeSpan duration) => Task.CompletedTask;
        public Task ResetLoginAttemptsAsync(string identifier) => Task.CompletedTask;
    }

    [Fact]
    public async Task RegisterSeller_ValidRequest_Returns201Created()
    {
        using var dbContext = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        dbContext.Users.Add(new Domain.Entities.User
        {
            Id = userId,
            Email = "seller_candidate@nexus.local",
            PasswordHash = "hash",
            FirstName = "Minh",
            LastName = "Tran",
            Status = UserStatus.Active
        });
        await dbContext.SaveChangesAsync();

        var sellerService = new SellerService(dbContext, new NoOpRedisOtpService(), null, NullLogger<SellerService>.Instance);
        var controller = new SellerController(sellerService, NullLogger<SellerController>.Instance);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["x-user-id"] = userId.ToString();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new CreateSellerRegistrationDto
        {
            ShopName = "Minh Shop",
            BusinessEmail = "minh@shop.local",
            Hotline = "+84911223344",
            IdentityCardNumber = "079201009999"
        };

        var result = await controller.RegisterSeller(request, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status201Created, objectResult.StatusCode);

        var responseDto = Assert.IsType<SellerRegistrationResponseDto>(objectResult.Value);
        Assert.Equal("PENDING", responseDto.Status);
        Assert.NotEmpty(responseDto.RegistrationId);
    }

    [Fact]
    public async Task RegisterSeller_MissingUserIdHeader_Returns400BadRequest()
    {
        using var dbContext = CreateInMemoryDbContext();
        var sellerService = new SellerService(dbContext, new NoOpRedisOtpService(), null, NullLogger<SellerService>.Instance);
        var controller = new SellerController(sellerService, NullLogger<SellerController>.Instance);

        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var request = new CreateSellerRegistrationDto
        {
            ShopName = "Minh Shop",
            BusinessEmail = "minh@shop.local",
            Hotline = "+84911223344",
            IdentityCardNumber = "079201009999"
        };

        var result = await controller.RegisterSeller(request, CancellationToken.None);

        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.NotNull(unauthorizedResult.Value);
    }

    [Fact]
    public async Task RegisterSeller_InvalidUserIdHeader_Returns401Unauthorized()
    {
        using var dbContext = CreateInMemoryDbContext();
        var sellerService = new SellerService(dbContext, new NoOpRedisOtpService(), null, NullLogger<SellerService>.Instance);
        var controller = new SellerController(sellerService, NullLogger<SellerController>.Instance);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["x-user-id"] = "not-a-valid-guid";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new CreateSellerRegistrationDto
        {
            ShopName = "Minh Shop",
            BusinessEmail = "minh@shop.local",
            Hotline = "+84911223344",
            IdentityCardNumber = "079201009999"
        };

        var result = await controller.RegisterSeller(request, CancellationToken.None);

        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.NotNull(unauthorizedResult.Value);
    }

    [Fact]
    public async Task ListSellerRegistrations_Returns200Ok_WithPagedResult()
    {
        using var dbContext = CreateInMemoryDbContext();
        var sellerService = new SellerService(dbContext, new NoOpRedisOtpService(), null, NullLogger<SellerService>.Instance);
        var controller = new AdminSellerRegistrationsController(sellerService, NullLogger<AdminSellerRegistrationsController>.Instance);

        var result = await controller.ListSellerRegistrations(1, 20, null, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var pagedResult = Assert.IsType<PagedData<AdminSellerRegistrationItemDto>>(okResult.Value);
        Assert.NotNull(pagedResult);
    }

    [Fact]
    public async Task ApproveSellerRegistration_Returns200Ok_WithApprovalResponse()
    {
        using var dbContext = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var user = new Domain.Entities.User
        {
            Id = userId,
            Email = "pending@nexus.local",
            PasswordHash = "hash",
            FirstName = "Van",
            LastName = "A",
            Status = UserStatus.Active
        };
        dbContext.Users.Add(user);

        var regId = Guid.NewGuid();
        dbContext.SellerProfiles.Add(new SellerProfile
        {
            Id = regId,
            UserId = userId,
            ShopName = "Test Shop To Approve",
            Hotline = "0901234567",
            PickupAddress = "Address 1",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "01",
            Status = SellerProfileStatus.Pending
        });
        await dbContext.SaveChangesAsync();

        var sellerService = new SellerService(dbContext, new NoOpRedisOtpService(), null, NullLogger<SellerService>.Instance);
        var controller = new AdminSellerRegistrationsController(sellerService, NullLogger<AdminSellerRegistrationsController>.Instance);

        var staffId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["x-user-id"] = staffId.ToString();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.ApproveSellerRegistration(regId, new ApproveSellerRegistrationDto { Note = "Approved" }, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApproveSellerRegistrationResponseDto>(okResult.Value);
        Assert.Equal("APPROVED", response.Status);
        Assert.Equal(regId.ToString(), response.RegistrationId);
    }

    [Fact]
    public async Task RejectSellerRegistration_Returns200Ok_WithRejectionResponse()
    {
        using var dbContext = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var user = new Domain.Entities.User
        {
            Id = userId,
            Email = "reject_candidate@nexus.local",
            PasswordHash = "hash",
            FirstName = "Van",
            LastName = "A",
            Status = UserStatus.Active
        };
        dbContext.Users.Add(user);

        var regId = Guid.NewGuid();
        dbContext.SellerProfiles.Add(new SellerProfile
        {
            Id = regId,
            UserId = userId,
            ShopName = "Test Shop To Reject",
            Hotline = "0901234567",
            PickupAddress = "Address 1",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "01",
            Status = SellerProfileStatus.Pending
        });
        await dbContext.SaveChangesAsync();

        var sellerService = new SellerService(dbContext, new NoOpRedisOtpService(), null, NullLogger<SellerService>.Instance);
        var controller = new AdminSellerRegistrationsController(sellerService, NullLogger<AdminSellerRegistrationsController>.Instance);

        var staffId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["x-user-id"] = staffId.ToString();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.RejectSellerRegistration(regId, new RejectSellerRegistrationDto { RejectionReason = "Invalid business license" }, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<RejectSellerRegistrationResponseDto>(okResult.Value);
        Assert.Equal("REJECTED", response.Status);
        Assert.Equal("Invalid business license", response.RejectionReason);
    }

    [Fact]
    public async Task GetSellerOwnProfile_ValidUserId_Returns200OkWithData()
    {
        using var dbContext = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var user = new Domain.Entities.User
        {
            Id = userId,
            Email = "seller_me@nexus.local",
            PasswordHash = "hash",
            FirstName = "Hoang",
            LastName = "Le",
            Status = UserStatus.Active
        };
        dbContext.Users.Add(user);

        var sellerProfile = new SellerProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ShopName = "Hoang Audio Store",
            Hotline = "+84909112233",
            PickupAddress = "99 Tran Hung Dao, District 5, HCMC",
            ProvinceId = 202,
            DistrictId = 1444,
            WardCode = "20202",
            Status = SellerProfileStatus.Approved
        };
        dbContext.SellerProfiles.Add(sellerProfile);
        await dbContext.SaveChangesAsync();

        var sellerService = new SellerService(dbContext, new NoOpRedisOtpService(), null, NullLogger<SellerService>.Instance);
        var controller = new SellerController(sellerService, NullLogger<SellerController>.Instance);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["x-user-id"] = userId.ToString();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.GetSellerOwnProfile(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<SellerOwnProfileResponseDto>(okResult.Value);
        Assert.Equal(sellerProfile.Id.ToString(), response.SellerId);
        Assert.Equal("Hoang Audio Store", response.ShopName);
        Assert.Equal("+84909112233", response.Hotline);
        Assert.Equal("APPROVED", response.Status);
    }

    [Fact]
    public async Task GetSellerOwnProfile_MissingOrInvalidUserId_Returns400BadRequest()
    {
        using var dbContext = CreateInMemoryDbContext();
        var sellerService = new SellerService(dbContext, new NoOpRedisOtpService(), null, NullLogger<SellerService>.Instance);
        var controller = new SellerController(sellerService, NullLogger<SellerController>.Instance);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["x-user-id"] = "invalid-uuid";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.GetSellerOwnProfile(CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task UpdateSellerOwnProfile_ValidPayload_Returns200Ok()
    {
        using var dbContext = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var user = new Domain.Entities.User
        {
            Id = userId,
            Email = "seller_patch@nexus.local",
            PasswordHash = "hash",
            FirstName = "An",
            LastName = "Vo",
            Status = UserStatus.Active
        };
        dbContext.Users.Add(user);

        var sellerProfile = new SellerProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ShopName = "An Vo Electronics",
            Hotline = "+84900111222",
            PickupAddress = "100 Pasteur, D1, HCMC",
            ProvinceId = 201,
            DistrictId = 1442,
            WardCode = "20101",
            Status = SellerProfileStatus.Approved
        };
        dbContext.SellerProfiles.Add(sellerProfile);
        await dbContext.SaveChangesAsync();

        var sellerService = new SellerService(dbContext, new NoOpRedisOtpService(), null, NullLogger<SellerService>.Instance);
        var controller = new SellerController(sellerService, NullLogger<SellerController>.Instance);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["x-user-id"] = userId.ToString();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var updateDto = new UpdateSellerOwnProfileDto
        {
            ShopName = "An Vo Gaming & Gear",
            Hotline = "+84900999888",
            PickupAddress = "200 Hai Ba Trung, D1, HCMC"
        };

        var result = await controller.UpdateSellerOwnProfile(updateDto, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<SellerOwnProfileUpdatedResponseDto>(okResult.Value);
        Assert.Equal(sellerProfile.Id.ToString(), response.ShopId);
        Assert.Equal(sellerProfile.Id.ToString(), response.SellerId);
        Assert.Equal("An Vo Gaming & Gear", response.ShopName);
    }

    [Fact]
    public async Task UpdateSellerOwnProfile_MissingOrInvalidUserId_Returns401Unauthorized()
    {
        using var dbContext = CreateInMemoryDbContext();
        var sellerService = new SellerService(dbContext, new NoOpRedisOtpService(), null, NullLogger<SellerService>.Instance);
        var controller = new SellerController(sellerService, NullLogger<SellerController>.Instance);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["x-user-id"] = "invalid-uuid";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.UpdateSellerOwnProfile(new UpdateSellerOwnProfileDto { ShopName = "Test" }, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }
}
