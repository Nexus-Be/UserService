namespace User.Tests;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using User.Api.Controllers;
using User.Application.Services.Admin;
using User.Domain.Entities;
using User.Domain.Enums;
using User.Infrastructure.Persistence;
using Xunit;

public sealed class AdminUsersControllerRestTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"admin_controller_test_db_{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task CreateUser_ValidRequest_Returns201Created()
    {
        using var dbContext = CreateInMemoryDbContext();
        dbContext.Roles.Add(new Role
        {
            Id = Guid.NewGuid(),
            RoleCode = RoleType.SupportStaff,
            RoleName = "Support Staff"
        });
        await dbContext.SaveChangesAsync();

        var adminService = new AdminUserService(dbContext, NullLogger<AdminUserService>.Instance);
        var controller = new AdminUsersController(adminService, NullLogger<AdminUsersController>.Instance);

        var adminId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["x-user-id"] = adminId.ToString();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new CreateAdminUserDto
        {
            Email = "new.staff@nexus.local",
            Password = "StaffPassword123!",
            FirstName = "Van",
            LastName = "Nguyen",
            PhoneNumber = "0909999999",
            RoleCodes = new List<string> { "staff" }
        };

        var result = await controller.CreateUser(request, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status201Created, objectResult.StatusCode);

        var response = Assert.IsType<AdminUserCreatedResponseDto>(objectResult.Value);
        Assert.NotNull(response);
        Assert.Equal("new.staff@nexus.local", response.Email);
        Assert.Contains("support staff", response.Roles);
    }

    [Fact]
    public async Task CreateUser_WithoutAdminHeader_SucceedsWithSystemAdmin()
    {
        using var dbContext = CreateInMemoryDbContext();
        dbContext.Roles.Add(new Role
        {
            Id = Guid.NewGuid(),
            RoleCode = RoleType.SupportStaff,
            RoleName = "Support Staff"
        });
        await dbContext.SaveChangesAsync();

        var adminService = new AdminUserService(dbContext, NullLogger<AdminUserService>.Instance);
        var controller = new AdminUsersController(adminService, NullLogger<AdminUsersController>.Instance);

        var httpContext = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new CreateAdminUserDto
        {
            Email = "noheader.staff@nexus.local",
            Password = "StaffPassword123!",
            FirstName = "Nam",
            LastName = "Le"
        };

        var result = await controller.CreateUser(request, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status201Created, objectResult.StatusCode);
    }

    [Fact]
    public async Task ListUsers_ValidQuery_Returns200OkWithPagedResult()
    {
        using var dbContext = CreateInMemoryDbContext();
        dbContext.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "listed.user@nexus.local",
            PasswordHash = "hash",
            FirstName = "Van",
            LastName = "Nguyen",
            Status = UserStatus.Active
        });
        await dbContext.SaveChangesAsync();

        var adminService = new AdminUserService(dbContext, NullLogger<AdminUserService>.Instance);
        var controller = new AdminUsersController(adminService, NullLogger<AdminUsersController>.Instance);

        var httpContext = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.ListUsers(page: 1, limit: 10, keyword: null, role: null, status: null, cancellationToken: CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);

        var pagedResult = Assert.IsType<AdminUserListPagedResultDto>(okResult.Value);
        Assert.Equal(1, pagedResult.Total);
        Assert.Single(pagedResult.Items);
        Assert.Equal("listed.user@nexus.local", pagedResult.Items[0].Email);
    }

    [Fact]
    public async Task GetUserDetail_ValidUserId_Returns200OkWithData()
    {
        using var dbContext = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        dbContext.Users.Add(new User
        {
            Id = userId,
            Email = "detail.user@nexus.local",
            PasswordHash = "hash",
            FirstName = "Hoang",
            LastName = "Le",
            Status = UserStatus.Active
        });
        await dbContext.SaveChangesAsync();

        var adminService = new AdminUserService(dbContext, NullLogger<AdminUserService>.Instance);
        var controller = new AdminUsersController(adminService, NullLogger<AdminUsersController>.Instance);

        var httpContext = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.GetUserDetail(userId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);

        var detail = Assert.IsType<AdminUserDetailDto>(okResult.Value);
        Assert.NotNull(detail);
        Assert.Equal(userId.ToString(), detail.Id);
        Assert.Equal("detail.user@nexus.local", detail.Email);
        Assert.Equal("Hoang Le", detail.DisplayName);
    }
}
