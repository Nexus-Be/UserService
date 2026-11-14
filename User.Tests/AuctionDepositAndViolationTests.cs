namespace User.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using User.Api.Common.Converters;
using User.Api.Controllers;
using User.Api.Controllers.Internal;
using User.Api.Filters;
using User.Api.Middleware;
using User.Application.Services.AuctionViolations;
using User.Application.Services.Internal;
using User.Domain.Entities;
using User.Domain.Enums;
using User.Domain.Exceptions;
using User.Infrastructure.Persistence;
using Xunit;

public sealed class AuctionDepositAndViolationTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"auction_deposit_tests_{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    private static IMemoryCache CreateMemoryCache() => new MemoryCache(new MemoryCacheOptions());

    private static void AttachUserHeader(ControllerBase controller, Guid userId, string roles)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["x-user-id"] = userId.ToString();
        httpContext.Request.Headers["x-user-roles"] = roles;
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    // =========================================================================
    // API #19: GET /api/v1/internal/users/{userId}/auction-deposit-profile
    // =========================================================================

    [Fact]
    public async Task GetAuctionDepositProfile_NewUserWithoutBuyerLogs_ReturnsIsNewUserTrueAndNullScore()
    {
        using var dbContext = CreateInMemoryDbContext();
        using var cache = CreateMemoryCache();
        var service = new InternalUserService(dbContext, cache, NullLogger<InternalUserService>.Instance);

        var userId = Guid.NewGuid();
        dbContext.Users.Add(new User
        {
            Id = userId,
            Email = "newbuyer@nexus.local",
            PasswordHash = "hash",
            FirstName = "New",
            LastName = "Buyer",
            Status = UserStatus.Active
        });
        dbContext.ReputationScores.Add(ReputationScore.CreateDefault(userId));
        await dbContext.SaveChangesAsync();

        var result = await service.GetAuctionDepositProfileAsync(userId);

        Assert.Equal(userId, result.UserId);
        Assert.Equal(UserStatus.Active, result.AccountStatus);
        Assert.True(result.IsNewUser);
        Assert.Null(result.ReputationScore);
        Assert.Equal(0, result.ViolationStreak);
        Assert.False(result.AuctionRestriction.IsRestricted);
        Assert.Null(result.AuctionRestriction.Type);
    }

    [Fact]
    public async Task GetAuctionDepositProfile_ExistingUserWithLogsAndExpiredRestriction_ReturnsScoreAndUnrestricted()
    {
        using var dbContext = CreateInMemoryDbContext();
        using var cache = CreateMemoryCache();
        var service = new InternalUserService(dbContext, cache, NullLogger<InternalUserService>.Instance);

        var userId = Guid.NewGuid();
        dbContext.Users.Add(new User
        {
            Id = userId,
            Email = "experienced@nexus.local",
            PasswordHash = "hash",
            FirstName = "Exp",
            LastName = "Buyer",
            Status = UserStatus.Active
        });
        dbContext.ReputationScores.Add(new ReputationScore
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CurrentScore = 88,
            TrustLevel = TrustLevel.Good
        });
        dbContext.ReputationLogs.Add(new ReputationLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TargetRole = TargetRole.Buyer,
            ChangeAmount = 1,
            ScoreAfter = 88,
            Reason = ReputationChangeReason.OrderCompleted,
            ReferenceType = ReferenceType.Order,
            ReferenceId = Guid.NewGuid().ToString()
        });
        dbContext.UserAuctionProfiles.Add(new UserAuctionProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ViolationStreak = 3,
            RestrictionType = AuctionRestrictionType.TempRestricted,
            RestrictionUntil = DateTimeOffset.UtcNow.AddMinutes(-5),
            RestrictionReason = "Expired restriction"
        });
        await dbContext.SaveChangesAsync();

        var result = await service.GetAuctionDepositProfileAsync(userId);

        Assert.False(result.IsNewUser);
        Assert.Equal(88, result.ReputationScore);
        Assert.Equal(3, result.ViolationStreak);
        Assert.False(result.AuctionRestriction.IsRestricted);
        Assert.Null(result.AuctionRestriction.Type);
        Assert.Null(result.AuctionRestriction.Until);
        Assert.Null(result.AuctionRestriction.Reason);
    }

    [Theory]
    [InlineData(UserStatus.Banned, false, UserStatus.Banned)]
    [InlineData(UserStatus.PendingVerification, false, UserStatus.PendingVerification)]
    [InlineData(UserStatus.Active, true, UserStatus.Deleted)]
    public async Task GetAuctionDepositProfile_MapsAccountStatusCorrectly(
        UserStatus dbStatus,
        bool isDeleted,
        UserStatus expectedStatus)
    {
        using var dbContext = CreateInMemoryDbContext();
        using var cache = CreateMemoryCache();
        var service = new InternalUserService(dbContext, cache, NullLogger<InternalUserService>.Instance);

        var userId = Guid.NewGuid();
        dbContext.Users.Add(new User
        {
            Id = userId,
            Email = $"{Guid.NewGuid():N}@nexus.local",
            PasswordHash = "hash",
            FirstName = "Test",
            LastName = "User",
            Status = dbStatus,
            IsDeleted = isDeleted
        });
        await dbContext.SaveChangesAsync();

        var result = await service.GetAuctionDepositProfileAsync(userId);

        Assert.Equal(expectedStatus, result.AccountStatus);
    }

    [Fact]
    public async Task GetAuctionDepositProfile_WhenUserDoesNotExist_ThrowsEntityNotFoundException()
    {
        using var dbContext = CreateInMemoryDbContext();
        using var cache = CreateMemoryCache();
        var service = new InternalUserService(dbContext, cache, NullLogger<InternalUserService>.Instance);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.GetAuctionDepositProfileAsync(Guid.NewGuid()));

        Assert.Equal("USER_NOT_FOUND", ex.ErrorCode);
        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public void AuctionDepositProfileResponse_SerializesEnumsAsPascalCase()
    {
        var dto = new AuctionDepositProfileResponse
        {
            UserId = Guid.NewGuid(),
            AccountStatus = UserStatus.Active,
            ReputationScore = 75,
            IsNewUser = false,
            ViolationStreak = 3,
            AuctionRestriction = new AuctionRestrictionDto
            {
                IsRestricted = true,
                Type = AuctionRestrictionType.TempRestricted,
                Until = DateTimeOffset.UtcNow.AddDays(7),
                Reason = "Test"
            }
        };

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new PascalCaseJsonStringEnumConverter(allowIntegerValues: false));

        var json = JsonSerializer.Serialize(dto, options);

        Assert.Contains("\"accountStatus\":\"Active\"", json);
        Assert.Contains("\"type\":\"TempRestricted\"", json);
    }

    // =========================================================================
    // API #20: POST /api/v1/internal/users/{userId}/auction-violations
    // =========================================================================

    [Fact]
    public async Task RecordAuctionViolation_EscalatesStreakAndSanctions_AndIsolatesFromNormalPurchase()
    {
        using var dbContext = CreateInMemoryDbContext();
        using var cache = CreateMemoryCache();
        var service = new InternalUserService(dbContext, cache, NullLogger<InternalUserService>.Instance);

        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "bidder@nexus.local",
            PasswordHash = "hash",
            FirstName = "Bid",
            LastName = "Der",
            Status = UserStatus.Active
        };
        dbContext.Users.Add(user);
        dbContext.ReputationScores.Add(new ReputationScore
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CurrentScore = 80,
            TrustLevel = TrustLevel.Good
        });
        await dbContext.SaveChangesAsync();

        // Violation 1 -> Warning (-5 => 75)
        var r1 = await service.RecordAuctionViolationAsync(userId, new RecordAuctionViolationRequest
        {
            ViolationType = "WINNER_NON_PAYMENT",
            AuctionId = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            OccurredAt = DateTimeOffset.UtcNow,
            DepositForfeitedAmount = 50000m
        });
        Assert.True(r1.Created);
        Assert.Equal(0, r1.PreviousStreak);
        Assert.Equal(1, r1.NewStreak);
        Assert.Equal(80, r1.ReputationBefore);
        Assert.Equal(75, r1.ReputationAfter);
        Assert.Equal(AuctionSanctionType.Warning, r1.Sanction.Type);

        // Violation 2 -> DepositRateIncreased (-10 => 65)
        var r2 = await service.RecordAuctionViolationAsync(userId, new RecordAuctionViolationRequest
        {
            ViolationType = "SecondChanceNonPayment",
            AuctionId = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            OccurredAt = DateTimeOffset.UtcNow,
            DepositForfeitedAmount = 0m
        });
        Assert.Equal(2, r2.NewStreak);
        Assert.Equal(65, r2.ReputationAfter);
        Assert.Equal(AuctionSanctionType.DepositRateIncreased, r2.Sanction.Type);

        // Violation 3 -> TempRestricted 7 days (-15 => 50)
        var r3 = await service.RecordAuctionViolationAsync(userId, new RecordAuctionViolationRequest
        {
            ViolationType = "WINNER_NON_PAYMENT",
            AuctionId = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            OccurredAt = DateTimeOffset.UtcNow,
            DepositForfeitedAmount = 100000m
        });
        Assert.Equal(3, r3.NewStreak);
        Assert.Equal(50, r3.ReputationAfter);
        Assert.Equal(AuctionSanctionType.TempRestricted, r3.Sanction.Type);
        Assert.NotNull(r3.Sanction.Until);

        // Violation 4 -> Suspended (-20 => 30)
        var r4 = await service.RecordAuctionViolationAsync(userId, new RecordAuctionViolationRequest
        {
            ViolationType = "WINNER_NON_PAYMENT",
            AuctionId = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            OccurredAt = DateTimeOffset.UtcNow,
            DepositForfeitedAmount = 100000m
        });
        Assert.Equal(4, r4.NewStreak);
        Assert.Equal(30, r4.ReputationAfter);
        Assert.Equal(AuctionSanctionType.Suspended, r4.Sanction.Type);
        Assert.Null(r4.Sanction.Until);

        // Isolation check: NO UserPenalty created, normal order placement MUST still be allowed!
        Assert.Empty(await dbContext.UserPenalties.Where(p => p.UserId == userId).ToListAsync());
        var reloadedUser = await dbContext.Users
            .Include(u => u.PenaltiesReceived)
            .FirstAsync(u => u.Id == userId);
        Assert.True(reloadedUser.CanPlaceOrder());
    }

    [Fact]
    public async Task RecordAuctionViolation_IdempotentDuplicate_Returns200WithOriginalSnapshot()
    {
        using var dbContext = CreateInMemoryDbContext();
        using var cache = CreateMemoryCache();
        var service = new InternalUserService(dbContext, cache, NullLogger<InternalUserService>.Instance);
        var controller = new InternalUsersController(null, NullLogger<InternalUsersController>.Instance, service);

        var userId = Guid.NewGuid();
        var auctionId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        dbContext.Users.Add(new User
        {
            Id = userId,
            Email = "idem@nexus.local",
            PasswordHash = "hash",
            FirstName = "Idem",
            LastName = "User",
            Status = UserStatus.Active
        });
        await dbContext.SaveChangesAsync();

        var request = new RecordAuctionViolationRequest
        {
            ViolationType = "WINNER_NON_PAYMENT",
            AuctionId = auctionId,
            OrderId = orderId,
            OccurredAt = DateTimeOffset.UtcNow,
            DepositForfeitedAmount = 25000m
        };

        var firstResult = await controller.RecordAuctionViolation(userId, request, CancellationToken.None);
        var createdObj = Assert.IsType<ObjectResult>(firstResult.Result);
        Assert.Equal(201, createdObj.StatusCode);
        var firstDto = Assert.IsType<RecordAuctionViolationResponse>(createdObj.Value);
        Assert.True(firstDto.Created);
        Assert.Equal(1, firstDto.NewStreak);

        var secondResult = await controller.RecordAuctionViolation(userId, request, CancellationToken.None);
        var okObj = Assert.IsType<OkObjectResult>(secondResult.Result);
        Assert.Equal(200, okObj.StatusCode);
        var secondDto = Assert.IsType<RecordAuctionViolationResponse>(okObj.Value);
        Assert.False(secondDto.Created);
        Assert.Equal(firstDto.ViolationId, secondDto.ViolationId);
        Assert.Equal(1, secondDto.NewStreak);
        Assert.Equal(firstDto.ReputationAfter, secondDto.ReputationAfter);
        Assert.Equal(1, await dbContext.AuctionViolations.CountAsync(v => v.UserId == userId));
    }

    [Fact]
    public async Task RecordAuctionViolation_InvalidViolationType_Throws422()
    {
        using var dbContext = CreateInMemoryDbContext();
        using var cache = CreateMemoryCache();
        var service = new InternalUserService(dbContext, cache, NullLogger<InternalUserService>.Instance);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.RecordAuctionViolationAsync(Guid.NewGuid(), new RecordAuctionViolationRequest
            {
                ViolationType = "UNKNOWN_TYPE",
                AuctionId = Guid.NewGuid(),
                OrderId = Guid.NewGuid(),
                OccurredAt = DateTimeOffset.UtcNow,
                DepositForfeitedAmount = 0m
            }));

        Assert.Equal("INVALID_VIOLATION_TYPE", ex.ErrorCode);
        Assert.Equal(422, ex.StatusCode);
    }

    // =========================================================================
    // API #21 & #22: AdminAuctionViolationsController & AuctionViolationAdminService
    // =========================================================================

    [Fact]
    public async Task GetAuctionViolations_PaginatesInSqlAndReturnsProfileSummary()
    {
        using var dbContext = CreateInMemoryDbContext();
        var adminService = new AuctionViolationAdminService(dbContext);

        var userId = Guid.NewGuid();
        dbContext.Users.Add(new User
        {
            Id = userId,
            Email = "history@nexus.local",
            PasswordHash = "hash",
            FirstName = "His",
            LastName = "Tory",
            Status = UserStatus.Active
        });
        dbContext.UserAuctionProfiles.Add(new UserAuctionProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ViolationStreak = 3,
            RestrictionType = AuctionRestrictionType.TempRestricted,
            RestrictionUntil = DateTimeOffset.UtcNow.AddDays(5),
            RestrictionReason = "Streak 3"
        });

        var baseTime = DateTimeOffset.UtcNow.AddHours(-10);
        for (var i = 1; i <= 5; i++)
        {
            dbContext.AuctionViolations.Add(new AuctionViolation
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ViolationType = AuctionViolationType.WinnerNonPayment,
                AuctionId = Guid.NewGuid(),
                OrderId = Guid.NewGuid(),
                OccurredAt = baseTime.AddHours(i),
                DepositForfeitedAmount = 10000m * i,
                Source = AuctionViolationSource.System,
                PreviousStreak = i - 1,
                NewStreak = i,
                ReputationBefore = 50,
                ReputationAfter = 45,
                SanctionType = AuctionSanctionType.Warning,
                CreatedAt = baseTime.AddHours(i),
                UpdatedAt = baseTime.AddHours(i)
            });
        }
        await dbContext.SaveChangesAsync();

        var page1 = await adminService.GetAuctionViolationsAsync(userId, page: 1, pageSize: 2);
        Assert.Equal(3, page1.CurrentStreak);
        Assert.True(page1.AuctionRestriction.IsRestricted);
        Assert.Equal(2, page1.Violations.Items.Count);
        Assert.Equal(5, page1.Violations.Pagination.TotalCount);
        Assert.Equal(3, page1.Violations.Pagination.TotalPages);
        Assert.True(page1.Violations.Pagination.HasNextPage);
        Assert.Equal(5, page1.Violations.Items[0].StreakAfter);
    }

    [Fact]
    public async Task AdjustAuctionViolation_ResetStreak_UpdatesProfileAndCreatesAuditLog()
    {
        using var dbContext = CreateInMemoryDbContext();
        var adminService = new AuctionViolationAdminService(dbContext);
        var controller = new AdminAuctionViolationsController(adminService, NullLogger<AdminAuctionViolationsController>.Instance);

        var buyerRoleId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        dbContext.Roles.Add(new Role
        {
            Id = buyerRoleId,
            RoleCode = RoleType.Buyer,
            RoleName = "Buyer"
        });
        dbContext.Users.Add(new User
        {
            Id = userId,
            Email = "appeal@nexus.local",
            PasswordHash = "hash",
            FirstName = "App",
            LastName = "Eal",
            Status = UserStatus.Active,
            RoleId = buyerRoleId
        });
        dbContext.UserAuctionProfiles.Add(new UserAuctionProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ViolationStreak = 3,
            RestrictionType = AuctionRestrictionType.TempRestricted,
            RestrictionUntil = DateTimeOffset.UtcNow.AddDays(5),
            RestrictionReason = "Streak 3"
        });
        await dbContext.SaveChangesAsync();

        AttachUserHeader(controller, adminId, "admin");
        var actionResult = await controller.AdjustAuctionViolation(
            userId,
            new AdjustAuctionViolationRequest
            {
                Action = AuctionAdminAdjustAction.ResetStreak,
                Reason = "Valid payment gateway outage appeal"
            },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<AdjustAuctionViolationResponse>(ok.Value);
        Assert.Equal(3, response.Before.Streak);
        Assert.Equal(0, response.After.Streak);
        Assert.NotEqual(Guid.Empty, response.AuditId);

        var audit = await dbContext.AuditLogs.FirstOrDefaultAsync(a => a.Id == response.AuditId);
        Assert.NotNull(audit);
        Assert.Equal(AuditAction.AuctionViolationAdjust, audit!.Action);
        Assert.Equal(adminId.ToString(), audit.UserId);
    }

    [Fact]
    public async Task AdjustAuctionViolation_OnAdminUser_Throws409CannotRestrictAdmin()
    {
        using var dbContext = CreateInMemoryDbContext();
        var adminService = new AuctionViolationAdminService(dbContext);

        var adminRoleId = Guid.NewGuid();
        var targetAdminId = Guid.NewGuid();
        dbContext.Roles.Add(new Role
        {
            Id = adminRoleId,
            RoleCode = RoleType.Admin,
            RoleName = "Admin"
        });
        dbContext.Users.Add(new User
        {
            Id = targetAdminId,
            Email = "admin@nexus.local",
            PasswordHash = "hash",
            FirstName = "Sys",
            LastName = "Admin",
            Status = UserStatus.Active,
            RoleId = adminRoleId
        });
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            adminService.AdjustAuctionViolationAsync(
                targetAdminId,
                new AdjustAuctionViolationRequest
                {
                    Action = AuctionAdminAdjustAction.Suspend,
                    Reason = "Should fail"
                },
                Guid.NewGuid()));

        Assert.Equal("CANNOT_RESTRICT_ADMIN", ex.ErrorCode);
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task AdjustAuctionViolation_NoChange_Throws409NothingToChange()
    {
        using var dbContext = CreateInMemoryDbContext();
        var adminService = new AuctionViolationAdminService(dbContext);

        var userId = Guid.NewGuid();
        dbContext.Users.Add(new User
        {
            Id = userId,
            Email = "nochange@nexus.local",
            PasswordHash = "hash",
            FirstName = "No",
            LastName = "Change",
            Status = UserStatus.Active
        });
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            adminService.AdjustAuctionViolationAsync(
                userId,
                new AdjustAuctionViolationRequest
                {
                    Action = AuctionAdminAdjustAction.ResetStreak,
                    Reason = "Already zero"
                },
                Guid.NewGuid()));

        Assert.Equal("NOTHING_TO_CHANGE", ex.ErrorCode);
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public void AdminAuctionViolationsController_EnforcesSegregatedRbacAttributes()
    {
        var controllerType = typeof(AdminAuctionViolationsController);
        var classRoleAttr = controllerType.GetCustomAttribute<RequireRoleAttribute>();
        Assert.NotNull(classRoleAttr);

        var adjustMethod = controllerType.GetMethod(nameof(AdminAuctionViolationsController.AdjustAuctionViolation));
        Assert.NotNull(adjustMethod);
        var adjustRoleAttr = adjustMethod!.GetCustomAttribute<RequireRoleAttribute>();
        Assert.NotNull(adjustRoleAttr);
    }

    [Fact]
    public async Task RecordAuctionViolation_EmptyViolationType_Throws400ValidationError()
    {
        using var dbContext = CreateInMemoryDbContext();
        using var cache = CreateMemoryCache();
        var service = new InternalUserService(dbContext, cache, NullLogger<InternalUserService>.Instance);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.RecordAuctionViolationAsync(Guid.NewGuid(), new RecordAuctionViolationRequest
            {
                ViolationType = "   ",
                AuctionId = Guid.NewGuid(),
                OrderId = Guid.NewGuid(),
                OccurredAt = DateTimeOffset.UtcNow,
                DepositForfeitedAmount = 0m
            }));

        Assert.Equal("VAL_INVALID_INPUT", ex.ErrorCode);
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task RecordAuctionViolation_Streak3_PreservesLongerExistingTempRestriction()
    {
        using var dbContext = CreateInMemoryDbContext();
        using var cache = CreateMemoryCache();
        var service = new InternalUserService(dbContext, cache, NullLogger<InternalUserService>.Instance);

        var userId = Guid.NewGuid();
        var longerUntil = DateTimeOffset.UtcNow.AddDays(30);
        dbContext.Users.Add(new User
        {
            Id = userId,
            Email = "longer@nexus.local",
            PasswordHash = "hash",
            FirstName = "Long",
            LastName = "Restriction",
            Status = UserStatus.Active
        });
        dbContext.UserAuctionProfiles.Add(new UserAuctionProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ViolationStreak = 2,
            RestrictionType = AuctionRestrictionType.TempRestricted,
            RestrictionUntil = longerUntil,
            RestrictionReason = "Admin 30-day restriction"
        });
        await dbContext.SaveChangesAsync();

        var result = await service.RecordAuctionViolationAsync(userId, new RecordAuctionViolationRequest
        {
            ViolationType = "WINNER_NON_PAYMENT",
            AuctionId = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            OccurredAt = DateTimeOffset.UtcNow,
            DepositForfeitedAmount = 50000m
        });

        Assert.Equal(3, result.NewStreak);
        Assert.Equal(AuctionSanctionType.TempRestricted, result.Sanction.Type);

        var reloadedProfile = await dbContext.UserAuctionProfiles.FirstAsync(p => p.UserId == userId);
        Assert.Equal(longerUntil, reloadedProfile.RestrictionUntil);
    }

    [Fact]
    public async Task AdjustAuctionViolation_AllActionsAndValidationErrors_BehaveCorrectly()
    {
        using var dbContext = CreateInMemoryDbContext();
        var adminService = new AuctionViolationAdminService(dbContext);

        var buyerRoleId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        dbContext.Roles.Add(new Role { Id = buyerRoleId, RoleCode = RoleType.Buyer, RoleName = "Buyer" });
        dbContext.Users.Add(new User
        {
            Id = userId,
            Email = "allactions@nexus.local",
            PasswordHash = "hash",
            FirstName = "All",
            LastName = "Actions",
            Status = UserStatus.Active,
            RoleId = buyerRoleId
        });
        await dbContext.SaveChangesAsync();

        // Validation errors: negative SetStreak, past ApplyRestriction, past Suspend
        var negEx = await Assert.ThrowsAsync<DomainException>(() =>
            adminService.AdjustAuctionViolationAsync(userId, new AdjustAuctionViolationRequest
            {
                Action = AuctionAdminAdjustAction.SetStreak,
                Value = -1,
                Reason = "Invalid negative streak"
            }, adminId));
        Assert.Equal("VAL_INVALID_INPUT", negEx.ErrorCode);
        Assert.Equal(400, negEx.StatusCode);

        var pastRestrictEx = await Assert.ThrowsAsync<DomainException>(() =>
            adminService.AdjustAuctionViolationAsync(userId, new AdjustAuctionViolationRequest
            {
                Action = AuctionAdminAdjustAction.ApplyRestriction,
                Until = DateTimeOffset.UtcNow.AddMinutes(-5),
                Reason = "Past timestamp"
            }, adminId));
        Assert.Equal("VAL_INVALID_INPUT", pastRestrictEx.ErrorCode);
        Assert.Equal(400, pastRestrictEx.StatusCode);

        var pastSuspendEx = await Assert.ThrowsAsync<DomainException>(() =>
            adminService.AdjustAuctionViolationAsync(userId, new AdjustAuctionViolationRequest
            {
                Action = AuctionAdminAdjustAction.Suspend,
                Until = DateTimeOffset.UtcNow.AddMinutes(-5),
                Reason = "Past suspend timestamp"
            }, adminId));
        Assert.Equal("VAL_INVALID_INPUT", pastSuspendEx.ErrorCode);
        Assert.Equal(400, pastSuspendEx.StatusCode);

        // SetStreak -> 2
        var setRes = await adminService.AdjustAuctionViolationAsync(userId, new AdjustAuctionViolationRequest
        {
            Action = AuctionAdminAdjustAction.SetStreak,
            Value = 2,
            Reason = "Manual streak alignment"
        }, adminId);
        Assert.Equal(0, setRes.Before.Streak);
        Assert.Equal(2, setRes.After.Streak);

        // ApplyRestriction -> TempRestricted
        var restrictUntil = DateTimeOffset.UtcNow.AddDays(10);
        var applyRes = await adminService.AdjustAuctionViolationAsync(userId, new AdjustAuctionViolationRequest
        {
            Action = AuctionAdminAdjustAction.ApplyRestriction,
            Until = restrictUntil,
            Reason = "Manual temporary restriction"
        }, adminId);
        Assert.Equal(AuctionRestrictionType.TempRestricted, applyRes.After.Restriction);
        Assert.Equal(restrictUntil, applyRes.After.RestrictionUntil);

        // LiftRestriction -> null
        var liftRes = await adminService.AdjustAuctionViolationAsync(userId, new AdjustAuctionViolationRequest
        {
            Action = AuctionAdminAdjustAction.LiftRestriction,
            Reason = "Appeal granted"
        }, adminId);
        Assert.Null(liftRes.After.Restriction);
        Assert.Null(liftRes.After.RestrictionUntil);

        // Suspend indefinitely -> Suspended, null Until
        var suspendRes = await adminService.AdjustAuctionViolationAsync(userId, new AdjustAuctionViolationRequest
        {
            Action = AuctionAdminAdjustAction.Suspend,
            Until = null,
            Reason = "Repeated abuse"
        }, adminId);
        Assert.Equal(AuctionRestrictionType.Suspended, suspendRes.After.Restriction);
        Assert.Null(suspendRes.After.RestrictionUntil);
    }

    [Fact]
    public async Task GlobalExceptionHandlerMiddleware_DomainException422_ReturnsRfc9110ProblemDetails()
    {
        var middleware = new GlobalExceptionHandlerMiddleware(
            _ => throw new DomainException("INVALID_VIOLATION_TYPE", "Auction violation type is invalid.", 422),
            NullLogger<GlobalExceptionHandlerMiddleware>.Instance);

        var context = new DefaultHttpContext();
        using var bodyStream = new MemoryStream();
        context.Response.Body = bodyStream;

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);

        bodyStream.Position = 0;
        using var doc = await JsonDocument.ParseAsync(bodyStream);
        var root = doc.RootElement;
        Assert.Equal("INVALID_VIOLATION_TYPE", root.GetProperty("errorCode").GetString());
        Assert.Equal(422, root.GetProperty("status").GetInt32());
        Assert.Equal("Unprocessable entity", root.GetProperty("title").GetString());
        Assert.Equal("https://tools.ietf.org/html/rfc9110#section-15.5.21", root.GetProperty("type").GetString());
    }

    [Fact]
    public void AdminAuctionViolationsController_RequireRoleFilter_EnforcesRuntimeRbac()
    {
        var controllerType = typeof(AdminAuctionViolationsController);
        var classFilter = controllerType.GetCustomAttribute<RequireRoleAttribute>()!;
        var adjustFilter = controllerType
            .GetMethod(nameof(AdminAuctionViolationsController.AdjustAuctionViolation))!
            .GetCustomAttribute<RequireRoleAttribute>()!;

        static int? RunFilter(RequireRoleAttribute filter, Guid? userId, string? roles)
        {
            var httpContext = new DefaultHttpContext();
            if (userId.HasValue)
            {
                httpContext.Request.Headers["x-user-id"] = userId.Value.ToString();
            }
            if (roles is not null)
            {
                httpContext.Request.Headers["x-user-roles"] = roles;
            }
            var authContext = new AuthorizationFilterContext(
                new ActionContext(httpContext, new RouteData(), new ActionDescriptor()),
                new List<IFilterMetadata>());
            filter.OnAuthorization(authContext);
            return (authContext.Result as ObjectResult)?.StatusCode;
        }

        var actorId = Guid.NewGuid();
        // Unauthenticated -> 401
        Assert.Equal(StatusCodes.Status401Unauthorized, RunFilter(classFilter, null, null));
        // Buyer -> 403 on both
        Assert.Equal(StatusCodes.Status403Forbidden, RunFilter(classFilter, actorId, "buyer"));
        // Staff -> allowed on GET (#21), 403 on POST adjust (#22)
        Assert.Null(RunFilter(classFilter, actorId, "staff"));
        Assert.Equal(StatusCodes.Status403Forbidden, RunFilter(adjustFilter, actorId, "staff"));
        // Admin -> allowed on both
        Assert.Null(RunFilter(classFilter, actorId, "admin"));
        Assert.Null(RunFilter(adjustFilter, actorId, "admin"));
    }

    [Fact]
    public async Task RecordAuctionViolation_SecondChanceNonPayment_WithPositiveDeposit_Throws400ValidationError()
    {
        using var db = CreateInMemoryDbContext();
        using var cache = CreateMemoryCache();
        var userId = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = userId,
            Email = $"sc-{userId:N}@example.com",
            PasswordHash = "hash",
            FirstName = "SecondChance",
            LastName = "Buyer",
            Status = UserStatus.Active
        });
        await db.SaveChangesAsync();

        var service = new InternalUserService(db, cache, NullLogger<InternalUserService>.Instance);
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.RecordAuctionViolationAsync(userId, new RecordAuctionViolationRequest
            {
                ViolationType = "SecondChanceNonPayment",
                AuctionId = Guid.NewGuid(),
                OrderId = Guid.NewGuid(),
                OccurredAt = DateTimeOffset.UtcNow,
                DepositForfeitedAmount = 10000m
            }));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal("VAL_INVALID_INPUT", ex.ErrorCode);
    }

    [Fact]
    public async Task AuctionEndpoints20To22_UserStatusDeletedWithoutIsDeleted_Throw404UserNotFound()
    {
        using var db = CreateInMemoryDbContext();
        using var cache = CreateMemoryCache();
        var userId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = userId,
            Email = $"del-{userId:N}@example.com",
            PasswordHash = "hash",
            FirstName = "Deleted",
            LastName = "Buyer",
            Status = UserStatus.Deleted,
            IsDeleted = false
        });
        db.Users.Add(new User
        {
            Id = adminId,
            Email = $"adm-{adminId:N}@example.com",
            PasswordHash = "hash",
            FirstName = "Admin",
            LastName = "User",
            Status = UserStatus.Active
        });
        await db.SaveChangesAsync();

        var internalService = new InternalUserService(db, cache, NullLogger<InternalUserService>.Instance);
        var adminService = new AuctionViolationAdminService(db);

        var ex20 = await Assert.ThrowsAsync<DomainException>(() =>
            internalService.RecordAuctionViolationAsync(userId, new RecordAuctionViolationRequest
            {
                ViolationType = "WinnerNonPayment",
                AuctionId = Guid.NewGuid(),
                OrderId = Guid.NewGuid(),
                OccurredAt = DateTimeOffset.UtcNow,
                DepositForfeitedAmount = 50000m
            }));
        Assert.Equal(404, ex20.StatusCode);
        Assert.Equal("USER_NOT_FOUND", ex20.ErrorCode);

        var ex21 = await Assert.ThrowsAsync<DomainException>(() =>
            adminService.GetAuctionViolationsAsync(userId));
        Assert.Equal(404, ex21.StatusCode);
        Assert.Equal("USER_NOT_FOUND", ex21.ErrorCode);

        var ex22 = await Assert.ThrowsAsync<DomainException>(() =>
            adminService.AdjustAuctionViolationAsync(userId, new AdjustAuctionViolationRequest
            {
                Action = AuctionAdminAdjustAction.ResetStreak,
                Reason = "Should fail for deleted status user"
            }, adminId));
        Assert.Equal(404, ex22.StatusCode);
        Assert.Equal("USER_NOT_FOUND", ex22.ErrorCode);
    }
}
