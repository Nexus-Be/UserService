namespace User.Tests;

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using User.Api.Extensions;
using User.Api.Filters;
using User.Domain.Entities;
using User.Domain.Enums;
using User.Domain.Exceptions;
using User.Infrastructure.Persistence;
using User.Application.Services.Seller;
using User.Application.Services.Admin;
using User.Application.Interfaces;
using User.Application.Models;
using Xunit;

public sealed class OneUserOneRoleAndHierarchicalPermissionsTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"one_user_one_role_test_db_{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public void RequireRoleAttribute_WhenEndpointRequiresBuyer_AllowsUserWithSellerRole()
    {
        // Arrange: User with single role "seller"
        var userId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, "seller")
        };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        var filter = new RequireRoleAttribute("buyer");
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var context = new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());

        // Act
        filter.OnAuthorization(context);

        // Assert: context.Result should be null (Authorized successfully)
        Assert.Null(context.Result);
    }

    [Fact]
    public void RequireRoleAttribute_WhenEndpointRequiresSeller_RejectsPureBuyer()
    {
        // Arrange: User with single role "buyer"
        var userId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, "buyer")
        };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        var filter = new RequireRoleAttribute("seller");
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var context = new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());

        // Act
        filter.OnAuthorization(context);

        // Assert: context.Result should be 403 Forbidden
        Assert.NotNull(context.Result);
        var objectResult = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    [Fact]
    public void HttpContextExtensions_HasRole_ReturnsTrueForBuyerWhenUserIsSeller()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, "seller")
        };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        // Act & Assert
        Assert.True(httpContext.HasRole("seller"));
        Assert.True(httpContext.HasRole("buyer")); // Seller inherits Buyer
        Assert.False(httpContext.HasRole("admin"));
        Assert.False(httpContext.HasRole("staff"));
    }

    [Fact]
    public void UserEntity_HasDefaultRoleId_PointingToBuyerRole()
    {
        // Arrange & Act
        var user = new User
        {
            Email = "newbie@nexus.local",
            PasswordHash = "hash",
            FirstName = "New",
            LastName = "User",
            Status = UserStatus.Active
        };

        // Assert
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), user.RoleId);
    }

    [Fact]
    public void SuspendedSeller_CanStillPlaceOrder_ButCannotSell()
    {
        // Arrange: User with SellerProfile in Suspended status
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "suspended_seller@nexus.local",
            PasswordHash = "hash",
            FirstName = "Shop",
            LastName = "Suspended",
            Status = UserStatus.Active,
            SellerProfile = new SellerProfile
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ShopName = "Suspended Shop",
                Status = SellerProfileStatus.Suspended,
                PickupAddress = "123 Warehouse St",
                ProvinceId = 1,
                DistrictId = 1,
                WardCode = "00001",
                Hotline = "19001234"
            }
        };

        // Penalized with TargetRole = Seller
        user.PenaltiesReceived.Add(new UserPenalty
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TargetRole = TargetRole.Seller,
            ViolationType = PenaltyViolationType.Other,
            Reason = "Operational shipping delay violation",
            PenaltyApplied = PenaltyType.Restriction,
            Status = PenaltyStatus.Active,
            IssuedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7)
        });

        // Act & Assert
        Assert.False(user.CanSell(), "Suspended seller must NOT be allowed to sell.");
        Assert.True(user.CanPlaceOrder(), "Suspended seller MUST be allowed to place orders as a buyer.");
    }

    [Fact]
    public void BannedAccount_CannotBuy_NorSell()
    {
        // Arrange: User with Status = Banned and TargetRole = Account penalty
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "banned_user@nexus.local",
            PasswordHash = "hash",
            FirstName = "Bad",
            LastName = "Actor",
            Status = UserStatus.Banned,
            SellerProfile = new SellerProfile
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ShopName = "Fraud Shop",
                Status = SellerProfileStatus.Suspended,
                PickupAddress = "456 Fraud St",
                ProvinceId = 2,
                DistrictId = 2,
                WardCode = "00002",
                Hotline = "19009999"
            }
        };

        user.PenaltiesReceived.Add(new UserPenalty
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TargetRole = TargetRole.Account,
            ViolationType = PenaltyViolationType.Fraud,
            Reason = "Severe fraud violation across platform",
            PenaltyApplied = PenaltyType.BanAccount,
            Status = PenaltyStatus.Active,
            IssuedAt = DateTimeOffset.UtcNow
        });

        // Act & Assert
        Assert.False(user.CanSell(), "Banned account must NOT be allowed to sell.");
        Assert.False(user.CanPlaceOrder(), "Banned account must NOT be allowed to place orders.");
    }

    [Fact]
    public void BuyerRestrictedUser_CannotBuy_ButShopCanSellIfApproved()
    {
        // Arrange: User with Approved shop, but received Buyer Restriction (e.g. COD cancellation)
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "honest_seller_bad_buyer@nexus.local",
            PasswordHash = "hash",
            FirstName = "Mixed",
            LastName = "Actor",
            Status = UserStatus.Active,
            SellerProfile = new SellerProfile
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ShopName = "Honest Shop",
                Status = SellerProfileStatus.Approved,
                PickupAddress = "789 Honest St",
                ProvinceId = 3,
                DistrictId = 3,
                WardCode = "00003",
                Hotline = "19008888"
            }
        };

        user.PenaltiesReceived.Add(new UserPenalty
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TargetRole = TargetRole.Buyer,
            ViolationType = PenaltyViolationType.CancellationWithoutReason,
            Reason = "Repeated COD order cancellations and abuses",
            PenaltyApplied = PenaltyType.Restriction,
            Status = PenaltyStatus.Active,
            IssuedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30)
        });

        // Act & Assert
        Assert.False(user.CanPlaceOrder(), "User with active buyer restriction must NOT be allowed to buy.");
        Assert.True(user.CanSell(), "User with approved shop and no seller penalties MUST be allowed to sell.");
    }

    [Fact]
    public async Task ApproveSellerRegistration_TransitionsRoleToSeller_AndSoftDeletesOldBuyerRole_AndRevokesRefreshTokens()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var fakeFulfillment = new FakeFulfillmentServiceClient();
        var service = new SellerService(dbContext, new DummyRedisOtpService(), null, fakeFulfillment, NullLogger<SellerService>.Instance);

        var buyerRoleId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var sellerRoleId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        dbContext.Roles.Add(new Role { Id = buyerRoleId, RoleCode = RoleType.Buyer, RoleName = "Buyer" });
        dbContext.Roles.Add(new Role { Id = sellerRoleId, RoleCode = RoleType.Seller, RoleName = "Seller" });

        var userId = Guid.NewGuid();
        var user = new Domain.Entities.User
        {
            Id = userId,
            Email = "candidate@nexus.local",
            PasswordHash = "hash",
            FirstName = "Van",
            LastName = "A",
            Status = UserStatus.Active,
            RoleId = buyerRoleId
        };
        dbContext.Users.Add(user);

        // User starts with single role Buyer in UserRoles
        var buyerUserRole = new UserRole
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RoleId = buyerRoleId,
            IsDeleted = false
        };
        dbContext.UserRoles.Add(buyerUserRole);

        // User has an active RefreshToken
        var activeRefreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = "hash123",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            IsRevoked = false
        };
        dbContext.RefreshTokens.Add(activeRefreshToken);

        var regId = Guid.NewGuid();
        var profile = new SellerProfile
        {
            Id = regId,
            UserId = userId,
            ShopName = "Candidate Shop",
            Hotline = "0901234567",
            PickupAddress = "123 Street",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "01",
            Status = SellerProfileStatus.Pending
        };
        dbContext.SellerProfiles.Add(profile);
        await dbContext.SaveChangesAsync();

        var staffId = Guid.NewGuid();

        // Act
        var response = await service.ApproveSellerRegistrationAsync(regId, staffId, new ApproveSellerRegistrationDto { Note = "Approved" }, CancellationToken.None);

        // Assert
        Assert.NotNull(response);
        var updatedUser = await dbContext.Users.FindAsync(userId);
        Assert.Equal(sellerRoleId, updatedUser!.RoleId);

        // 1 User 1 Role: Active UserRoles must have ONLY SellerRole
        var activeUserRoles = await dbContext.UserRoles
            .Where(ur => ur.UserId == userId && !ur.IsDeleted)
            .ToListAsync();
        Assert.Single(activeUserRoles);
        Assert.Equal(sellerRoleId, activeUserRoles[0].RoleId);

        // Buyer role must be soft-deleted
        var softDeletedBuyerRole = await dbContext.UserRoles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == buyerRoleId);
        Assert.NotNull(softDeletedBuyerRole);
        Assert.True(softDeletedBuyerRole.IsDeleted);
        Assert.NotNull(softDeletedBuyerRole.DeletedAt);

        // Active refresh tokens must be revoked
        var tokenInDb = await dbContext.RefreshTokens.FindAsync(activeRefreshToken.Id);
        Assert.NotNull(tokenInDb);
        Assert.True(tokenInDb.IsRevoked);
        Assert.NotNull(tokenInDb.RevokedAt);
    }

    [Fact]
    public async Task AdminUserDirectory_GetUserDetail_WhenSellerHasSoftDeletedBuyerRole_ReturnsOnlySellerRole()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var adminUserService = new AdminUserService(dbContext, NullLogger<AdminUserService>.Instance);

        var buyerRoleId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var sellerRoleId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var buyerRole = new Role { Id = buyerRoleId, RoleCode = RoleType.Buyer, RoleName = "Buyer" };
        var sellerRole = new Role { Id = sellerRoleId, RoleCode = RoleType.Seller, RoleName = "Seller" };
        dbContext.Roles.AddRange(buyerRole, sellerRole);

        var userId = Guid.NewGuid();
        var user = new Domain.Entities.User
        {
            Id = userId,
            Email = "seller_detail@nexus.local",
            PasswordHash = "hash",
            FirstName = "Shop",
            LastName = "Owner",
            Status = UserStatus.Active,
            RoleId = sellerRoleId
        };
        dbContext.Users.Add(user);

        // Active Seller role
        dbContext.UserRoles.Add(new UserRole
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RoleId = sellerRoleId,
            Role = sellerRole,
            IsDeleted = false
        });

        // Soft-deleted Buyer role
        dbContext.UserRoles.Add(new UserRole
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RoleId = buyerRoleId,
            Role = buyerRole,
            IsDeleted = true,
            DeletedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync();

        // Act
        var detail = await adminUserService.GetUserDetailAsync(userId, CancellationToken.None);

        // Assert: Admin detail must contain ONLY "seller"
        Assert.NotNull(detail);
        Assert.Single(detail.Roles);
        Assert.Equal("seller", detail.Roles[0]);
    }

    [Fact]
    public async Task AdminAssignRoles_WhenRevokingSellerFromSuspendedShop_RestoresBuyerRoleAsSingleActiveRole_AndPreservesProfile()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var adminService = new AdminUserService(dbContext, NullLogger<AdminUserService>.Instance);

        var buyerRoleId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var sellerRoleId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var buyerRole = new Role { Id = buyerRoleId, RoleCode = RoleType.Buyer, RoleName = "Buyer" };
        var sellerRole = new Role { Id = sellerRoleId, RoleCode = RoleType.Seller, RoleName = "Seller" };
        dbContext.Roles.AddRange(buyerRole, sellerRole);

        var targetUserId = Guid.NewGuid();
        var user = new Domain.Entities.User
        {
            Id = targetUserId,
            Email = "suspended_to_buyer@nexus.local",
            PasswordHash = "hash",
            FirstName = "Shop",
            LastName = "Owner",
            Status = UserStatus.Active,
            RoleId = sellerRoleId
        };
        dbContext.Users.Add(user);

        // Seller role is active
        var sellerUr = new UserRole { Id = Guid.NewGuid(), UserId = targetUserId, RoleId = sellerRoleId, Role = sellerRole, IsDeleted = false };
        // Buyer role was previously soft-deleted when upgraded
        var buyerUr = new UserRole { Id = Guid.NewGuid(), UserId = targetUserId, RoleId = buyerRoleId, Role = buyerRole, IsDeleted = true, DeletedAt = DateTimeOffset.UtcNow.AddDays(-10) };
        dbContext.UserRoles.AddRange(sellerUr, buyerUr);

        var sellerProfile = new SellerProfile
        {
            Id = Guid.NewGuid(),
            UserId = targetUserId,
            ShopName = "Suspended Store",
            PickupAddress = "123 Storage",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "01",
            Hotline = "19001111",
            Status = SellerProfileStatus.Suspended
        };
        dbContext.SellerProfiles.Add(sellerProfile);
        await dbContext.SaveChangesAsync();

        var dto = new AssignAdminUserRolesDto
        {
            RoleCodes = new List<string> { "buyer" },
            Reason = "Admin demotes suspended seller back to pure buyer"
        };
        var adminId = Guid.NewGuid();

        // Act
        var response = await adminService.AssignUserRolesAsync(targetUserId, dto, adminId, CancellationToken.None);

        // Assert
        Assert.NotNull(response);
        Assert.Single(response.Roles);
        Assert.Equal("buyer", response.Roles[0]);

        // User.RoleId updated
        var updatedUser = await dbContext.Users.FindAsync(targetUserId);
        Assert.Equal(buyerRoleId, updatedUser!.RoleId);

        // In DB: UserRoles active has ONLY 1 role (Buyer)
        var activeRoles = await dbContext.UserRoles.Where(ur => ur.UserId == targetUserId && !ur.IsDeleted).ToListAsync();
        Assert.Single(activeRoles);
        Assert.Equal(buyerRoleId, activeRoles[0].RoleId);

        // Seller role is soft-deleted
        var softDeletedSeller = await dbContext.UserRoles.IgnoreQueryFilters().FirstOrDefaultAsync(ur => ur.UserId == targetUserId && ur.RoleId == sellerRoleId);
        Assert.NotNull(softDeletedSeller);
        Assert.True(softDeletedSeller.IsDeleted);

        // SellerProfile is still preserved in Suspended state
        var profileInDb = await dbContext.SellerProfiles.FindAsync(sellerProfile.Id);
        Assert.NotNull(profileInDb);
        Assert.Equal(SellerProfileStatus.Suspended, profileInDb.Status);
        Assert.False(profileInDb.IsDeleted);

        // GetUserDetailAsync returns only ["buyer"]
        var detail = await adminService.GetUserDetailAsync(targetUserId, CancellationToken.None);
        Assert.Single(detail.Roles);
        Assert.Equal("buyer", detail.Roles[0]);
    }

    [Fact]
    public async Task AdminAssignRoles_WhenAttemptingToRevokeSellerFromApprovedShop_ThrowsCannotRevokeSellerRoleDirectly()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var adminService = new AdminUserService(dbContext, NullLogger<AdminUserService>.Instance);

        var buyerRoleId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var sellerRoleId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var buyerRole = new Role { Id = buyerRoleId, RoleCode = RoleType.Buyer, RoleName = "Buyer" };
        var sellerRole = new Role { Id = sellerRoleId, RoleCode = RoleType.Seller, RoleName = "Seller" };
        dbContext.Roles.AddRange(buyerRole, sellerRole);

        var targetUserId = Guid.NewGuid();
        var user = new Domain.Entities.User
        {
            Id = targetUserId,
            Email = "approved_seller@nexus.local",
            PasswordHash = "hash",
            FirstName = "Active",
            LastName = "Seller",
            Status = UserStatus.Active,
            RoleId = sellerRoleId
        };
        dbContext.Users.Add(user);

        dbContext.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = targetUserId, RoleId = sellerRoleId, Role = sellerRole, IsDeleted = false });

        var sellerProfile = new SellerProfile
        {
            Id = Guid.NewGuid(),
            UserId = targetUserId,
            ShopName = "Active Store",
            PickupAddress = "123 Storage",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "01",
            Hotline = "19001111",
            Status = SellerProfileStatus.Approved // Active, not suspended!
        };
        dbContext.SellerProfiles.Add(sellerProfile);
        await dbContext.SaveChangesAsync();

        var dto = new AssignAdminUserRolesDto
        {
            RoleCodes = new List<string> { "buyer" },
            Reason = "Attempt to directly strip seller role from active shop"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            adminService.AssignUserRolesAsync(targetUserId, dto, Guid.NewGuid(), CancellationToken.None));

        Assert.Equal("CANNOT_REVOKE_SELLER_ROLE_DIRECTLY", ex.ErrorCode);
    }

    [Fact]
    public async Task AdminAssignRoles_WhenAttemptingToAssignMultipleRoles_ThrowsMultipleRolesNotAllowed()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var adminService = new AdminUserService(dbContext, NullLogger<AdminUserService>.Instance);

        var buyerRoleId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var staffRoleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var buyerRole = new Role { Id = buyerRoleId, RoleCode = RoleType.Buyer, RoleName = "Buyer", IsActive = true };
        var staffRole = new Role { Id = staffRoleId, RoleCode = RoleType.SupportStaff, RoleName = "SupportStaff", IsActive = true };
        dbContext.Roles.AddRange(buyerRole, staffRole);

        var targetUserId = Guid.NewGuid();
        var user = new Domain.Entities.User
        {
            Id = targetUserId,
            Email = "buyer@nexus.com",
            PasswordHash = "hash",
            FirstName = "Target",
            LastName = "User",
            Status = UserStatus.Active,
            RoleId = buyerRoleId
        };
        dbContext.Users.Add(user);
        dbContext.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = targetUserId, RoleId = buyerRoleId, Role = buyerRole, IsDeleted = false });
        await dbContext.SaveChangesAsync();

        var dto = new AssignAdminUserRolesDto
        {
            RoleCodes = new List<string> { "buyer", "staff" },
            Reason = "Attempt to assign multiple roles"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            adminService.AssignUserRolesAsync(targetUserId, dto, Guid.NewGuid(), CancellationToken.None));

        Assert.Equal("MULTIPLE_ROLES_NOT_ALLOWED", ex.ErrorCode);
    }

    [Fact]
    public async Task AdminCreateUser_WhenAttemptingToAssignMultipleRoles_ThrowsMultipleRolesNotAllowed()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var adminService = new AdminUserService(dbContext, NullLogger<AdminUserService>.Instance);

        var buyerRoleId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var staffRoleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var buyerRole = new Role { Id = buyerRoleId, RoleCode = RoleType.Buyer, RoleName = "Buyer", IsActive = true };
        var staffRole = new Role { Id = staffRoleId, RoleCode = RoleType.SupportStaff, RoleName = "SupportStaff", IsActive = true };
        dbContext.Roles.AddRange(buyerRole, staffRole);
        await dbContext.SaveChangesAsync();

        var dto = new CreateAdminUserDto
        {
            Email = "newstaff@nexus.com",
            Password = "Password123!",
            FirstName = "Staff",
            LastName = "Member",
            RoleCodes = new List<string> { "buyer", "staff" }
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            adminService.CreateUserAsync(dto, Guid.NewGuid(), CancellationToken.None));

        Assert.Equal("MULTIPLE_ROLES_NOT_ALLOWED", ex.ErrorCode);
    }

    private sealed class FakeFulfillmentServiceClient : IFulfillmentServiceClient
    {
        public Task<bool> CreateWarehouseAsync(Guid sellerId, Guid userId, string shopName, string contactName, string contactPhone, string pickupAddress, int provinceId, int districtId, string wardCode, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<bool> UpdateWarehouseAsync(Guid sellerId, string shopName, string contactPhone, string pickupAddress, int provinceId, int districtId, string wardCode, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<bool> DeleteWarehouseBySellerIdAsync(Guid sellerId, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }

    private sealed class DummyRedisOtpService : IRedisOtpService
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
}
