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

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequestResult.Value);
    }

    [Fact]
    public async Task RegisterSeller_InvalidUserIdHeader_Returns400BadRequest()
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

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequestResult.Value);
    }
}
