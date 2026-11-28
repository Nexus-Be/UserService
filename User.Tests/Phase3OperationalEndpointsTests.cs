namespace User.Tests;

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using User.Api.Common;
using User.Api.Controllers;
using User.Api.Controllers.Internal;
using User.Application.Common;
using User.Application.Common.Models;
using User.Application.Services.Internal;
using User.Application.Services.Report;
using User.Application.Services.Seller;
using User.Domain.Entities;
using User.Domain.Enums;
using User.Domain.Exceptions;
using User.Infrastructure.Persistence;
using Xunit;

public sealed class Phase3OperationalEndpointsTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"phase3_test_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static IConfiguration BuildConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["InternalSecurity:ServiceKey"] = "sec" })
            .Build();

    [Fact]
    public async Task Internal_GetUserAndBatchSellers_WorkCorrectly()
    {
        using var db = CreateDbContext();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var roleId = Guid.NewGuid();
        db.Roles.Add(new Role { Id = roleId, RoleName = "Seller", RoleCode = RoleType.Seller });
        var userId = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = userId,
            Email = "seller@nexus.local",
            PasswordHash = "hash",
            FirstName = "Le",
            LastName = "Minh",
            PhoneNumber = "0909999999",
            Status = UserStatus.Active,
            RoleId = roleId
        });
        var sellerId = Guid.NewGuid();
        db.SellerProfiles.Add(new SellerProfile
        {
            Id = sellerId,
            UserId = userId,
            ShopName = "Minh Electronics",
            PickupAddress = "101 Nguyen Hue",
            ProvinceId = 202,
            DistrictId = 1442,
            WardCode = "20101",
            Hotline = "0909999999",
            Status = SellerProfileStatus.Approved
        });
        await db.SaveChangesAsync();

        var service = new InternalUserService(db, cache, NullLogger<InternalUserService>.Instance);
        var ctrl = new InternalUsersController(null, NullLogger<InternalUsersController>.Instance, service);
        ctrl.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        ctrl.ControllerContext.HttpContext.Request.Headers["X-Internal-Service-Key"] = "sec";

        var userRes = Assert.IsType<OkObjectResult>(await ctrl.GetUserById(userId, BuildConfig(), CancellationToken.None));
        Assert.NotNull(userRes.Value);

        var notFoundUser = await ctrl.GetUserById(Guid.NewGuid(), BuildConfig(), CancellationToken.None);
        Assert.IsType<NotFoundResult>(notFoundUser);

        var batchRes = Assert.IsType<OkObjectResult>(await ctrl.GetBatchSellerInfo(
            new BatchSellerInfoRequest { UserIds = new[] { userId } }, BuildConfig(), CancellationToken.None));
        Assert.NotNull(batchRes.Value);
    }

    [Fact]
    public async Task MeAndAdmin_ReportDetail_EnforceOwnershipAndReturnDetail()
    {
        using var db = CreateDbContext();
        var reporterId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var reportedId = Guid.NewGuid();
        db.Users.AddRange(
            new User { Id = reporterId, Email = "r1@nexus.local", PasswordHash = "h", FirstName = "R", LastName = "1", Status = UserStatus.Active },
            new User { Id = otherId, Email = "r2@nexus.local", PasswordHash = "h", FirstName = "R", LastName = "2", Status = UserStatus.Active },
            new User { Id = reportedId, Email = "bad@nexus.local", PasswordHash = "h", FirstName = "B", LastName = "1", Status = UserStatus.Active });

        var reportId = Guid.NewGuid();
        db.UserReports.Add(new UserReport
        {
            Id = reportId,
            ReporterId = reporterId,
            ReportedUserId = reportedId,
            ViolationType = ReportViolationType.Fraud,
            Description = "Fake item",
            Status = ReportStatus.Pending,
            EvidenceUrls = new List<string> { "https://img/1.png" }
        });
        await db.SaveChangesAsync();

        var reportService = new ReportService(db, NullLogger<ReportService>.Instance);

        var meCtrl = new MeController(null!, NullLogger<MeController>.Instance, reportService: reportService);
        var http = new DefaultHttpContext();
        http.Request.Headers["x-user-id"] = reporterId.ToString();
        http.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", reporterId.ToString()) }, "Test"));
        meCtrl.ControllerContext = new ControllerContext { HttpContext = http };

        var meOk = Assert.IsType<OkObjectResult>(await meCtrl.GetMyReportById(reportId, CancellationToken.None));
        var meDto = Assert.IsType<ReportListItemDto>(meOk.Value);
        Assert.Equal(reportId, meDto.ReportId);

        await Assert.ThrowsAsync<DomainException>(() => reportService.GetMyReportByIdAsync(otherId, reportId, CancellationToken.None));

        var adminCtrl = new AdminReportsController(reportService, NullLogger<AdminReportsController>.Instance);
        var adminOk = Assert.IsType<OkObjectResult>(await adminCtrl.GetAdminReportById(reportId, CancellationToken.None));
        Assert.Equal(reportId, Assert.IsType<ReportListItemDto>(adminOk.Value).ReportId);
    }

    [Fact]
    public async Task AdminSellersController_ListSellers_FiltersAndPaginates()
    {
        using var db = CreateDbContext();
        var u1 = Guid.NewGuid();
        var u2 = Guid.NewGuid();
        db.Users.AddRange(
            new User { Id = u1, Email = "s1@nexus.local", PasswordHash = "h", FirstName = "S", LastName = "1", Status = UserStatus.Active },
            new User { Id = u2, Email = "s2@nexus.local", PasswordHash = "h", FirstName = "S", LastName = "2", Status = UserStatus.Active });
        db.SellerProfiles.AddRange(
            new SellerProfile { Id = Guid.NewGuid(), UserId = u1, ShopName = "Alpha Store", PickupAddress = "A", ProvinceId = 1, DistrictId = 1, WardCode = "W", Hotline = "0901", Status = SellerProfileStatus.Approved },
            new SellerProfile { Id = Guid.NewGuid(), UserId = u2, ShopName = "Beta Store", PickupAddress = "B", ProvinceId = 1, DistrictId = 1, WardCode = "W", Hotline = "0902", Status = SellerProfileStatus.Suspended });
        await db.SaveChangesAsync();

        var sellerService = new SellerService(db, null!, null, null!, NullLogger<SellerService>.Instance);
        var ctrl = new AdminSellersController(sellerService, NullLogger<AdminSellersController>.Instance);

        var ok = Assert.IsType<OkObjectResult>(await ctrl.ListAdminSellers(new PageQuery { Page = 1, PageSize = 20 }, SellerProfileStatus.Suspended, "Beta", CancellationToken.None));
        var paged = Assert.IsType<PagedData<AdminSellerListItemDto>>(ok.Value);
        Assert.Single(paged.Items);
        Assert.Equal("Beta Store", paged.Items[0].ShopName);
    }
}
