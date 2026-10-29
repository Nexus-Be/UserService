namespace User.Tests;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using User.Api.Controllers;
using User.Application.Services.Profile;
using User.Domain.Entities;
using User.Domain.Enums;
using User.Infrastructure.Persistence;
using Xunit;

public sealed class UsersControllerRestTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"user_profile_api_test_db_{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task GetCurrentUserProfile_With_Valid_Header_Returns_Ok()
    {
        using var dbContext = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var user = new Domain.Entities.User
        {
            Id = userId,
            Username = "johndoe",
            Email = "john@nexus.local",
            PasswordHash = "hash",
            FirstName = "John",
            LastName = "Doe",
            Status = UserStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var profileService = new ProfileService(dbContext, NullLogger<ProfileService>.Instance);
        var controller = new UsersController(profileService, NullLogger<UsersController>.Instance);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["x-user-id"] = userId.ToString();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        var result = await controller.GetCurrentUserProfile(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var profile = Assert.IsType<UserProfileDto>(okResult.Value);
        Assert.Equal("john@nexus.local", profile.Email);
        Assert.Equal("John Doe", profile.DisplayName);
    }

    [Fact]
    public async Task GetCurrentUserProfile_Without_Header_Returns_BadRequest()
    {
        using var dbContext = CreateInMemoryDbContext();
        var profileService = new ProfileService(dbContext, NullLogger<ProfileService>.Instance);
        var controller = new UsersController(profileService, NullLogger<UsersController>.Instance);

        var httpContext = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        var result = await controller.GetCurrentUserProfile(CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UpdateCurrentUserProfile_Updates_Data_And_Returns_Ok()
    {
        using var dbContext = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var user = new Domain.Entities.User
        {
            Id = userId,
            Username = "alex",
            Email = "alex@nexus.local",
            PasswordHash = "hash",
            FirstName = "Alex",
            LastName = "Smith",
            Status = UserStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var profileService = new ProfileService(dbContext, NullLogger<ProfileService>.Instance);
        var controller = new UsersController(profileService, NullLogger<UsersController>.Instance);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["x-user-id"] = userId.ToString();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        var updateRequest = new UpdateProfileApiRequest(
            DisplayName: "Alexander TheGreat",
            PhoneNumber: "0999888777"
        );

        var result = await controller.UpdateCurrentUserProfile(updateRequest, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var profile = Assert.IsType<UserProfileDto>(okResult.Value);
        Assert.Equal("Alexander", profile.FirstName);
        Assert.Equal("TheGreat", profile.LastName);
        Assert.Equal("0999888777", profile.PhoneNumber);
    }

    [Fact]
    public void UpdateProfileApiRequest_Deserializes_Object_Address_Correctly()
    {
        var json = """
        {
            "displayName": "Test User",
            "address": {
                "streetAddress": "123 Le Loi",
                "wardCode": "001",
                "districtId": 2,
                "provinceId": 1
            }
        }
        """;

        var request = System.Text.Json.JsonSerializer.Deserialize<UpdateProfileApiRequest>(json, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(request);
        Assert.Equal("Test User", request.DisplayName);
        Assert.NotNull(request.Address);
        Assert.Contains("123 Le Loi", request.Address);
        Assert.Contains("001", request.Address);
    }

    [Fact]
    public void UpdateProfileApiRequest_Deserializes_String_Address_Correctly()
    {
        var json = """
        {
            "displayName": "Test User",
            "address": "456 Nguyen Trai, Ha Noi"
        }
        """;

        var request = System.Text.Json.JsonSerializer.Deserialize<UpdateProfileApiRequest>(json, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(request);
        Assert.Equal("456 Nguyen Trai, Ha Noi", request.Address);
    }
}
