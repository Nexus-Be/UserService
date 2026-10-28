namespace User.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using User.Application.Services.Admin;
using User.Domain.Entities;
using User.Domain.Enums;
using User.Domain.Exceptions;
using User.Infrastructure.Persistence;
using Xunit;

public sealed class AdminUserServiceTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"admin_user_test_db_{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    private static async Task SeedSystemRolesAsync(AppDbContext dbContext)
    {
        if (!await dbContext.Roles.AnyAsync())
        {
            dbContext.Roles.AddRange(
                new Role { Id = Guid.NewGuid(), RoleCode = RoleType.Buyer, RoleName = "Buyer" },
                new Role { Id = Guid.NewGuid(), RoleCode = RoleType.Seller, RoleName = "Seller" },
                new Role { Id = Guid.NewGuid(), RoleCode = RoleType.SupportStaff, RoleName = "Support Staff" },
                new Role { Id = Guid.NewGuid(), RoleCode = RoleType.Admin, RoleName = "Administrator" }
            );
            await dbContext.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task CreateUserAsync_ValidSupportStaff_Success()
    {
        using var dbContext = CreateInMemoryDbContext();
        await SeedSystemRolesAsync(dbContext);
        var service = new AdminUserService(dbContext, NullLogger<AdminUserService>.Instance);

        var adminId = Guid.NewGuid();
        var dto = new CreateAdminUserDto
        {
            Email = "staff.test@nexus.local",
            Password = "StaffPassword123!",
            FirstName = "Van",
            LastName = "Nguyen",
            PhoneNumber = "0901234567",
            RoleCodes = new List<string> { "staff" }
        };

        var response = await service.CreateUserAsync(dto, adminId, CancellationToken.None);

        Assert.NotNull(response);
        Assert.True(Guid.TryParse(response.UserId, out var userId));
        Assert.Equal("staff.test@nexus.local", response.Email);
        Assert.Contains("support staff", response.Roles);
        Assert.Equal("Active", response.Status);

        // Verify User entity in DB
        var user = await dbContext.Users.FindAsync(userId);
        Assert.NotNull(user);
        Assert.Equal("staff.test@nexus.local", user.Email);
        Assert.Equal("Van", user.FirstName);
        Assert.Equal("Nguyen", user.LastName);
        Assert.Equal("0901234567", user.PhoneNumber);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.NotNull(user.EmailVerifiedAt);

        // Verify password is hashed with BCrypt
        Assert.NotEqual("StaffPassword123!", user.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify("StaffPassword123!", user.PasswordHash));

        // Verify UserRole
        var userRoles = await dbContext.UserRoles.Where(ur => ur.UserId == userId).ToListAsync();
        Assert.Single(userRoles);
        Assert.Equal(adminId, userRoles[0].AssignedBy);

        // Verify ReputationScore initialized
        var repScore = await dbContext.ReputationScores.FirstOrDefaultAsync(r => r.UserId == userId);
        Assert.NotNull(repScore);
        Assert.Equal(50, repScore.CurrentScore);
        Assert.Equal(TrustLevel.Standard, repScore.TrustLevel);

        // Verify AuditLog
        var audit = await dbContext.AuditLogs.FirstOrDefaultAsync(a => a.EntityId == userId.ToString() && a.Action == AuditAction.RoleAssign);
        Assert.NotNull(audit);
        Assert.Equal(adminId.ToString(), audit.UserId);
    }

    [Fact]
    public async Task CreateUserAsync_DuplicateEmail_ThrowsDomainException()
    {
        using var dbContext = CreateInMemoryDbContext();
        await SeedSystemRolesAsync(dbContext);
        var service = new AdminUserService(dbContext, NullLogger<AdminUserService>.Instance);

        dbContext.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "existing@nexus.local",
            PasswordHash = "hash",
            FirstName = "Existing",
            LastName = "User",
            Status = UserStatus.Active
        });
        await dbContext.SaveChangesAsync();

        var dto = new CreateAdminUserDto
        {
            Email = "existing@nexus.local",
            Password = "Password123!",
            FirstName = "New",
            LastName = "User"
        };

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.CreateUserAsync(dto, Guid.NewGuid(), CancellationToken.None));

        Assert.Equal("EMAIL_ALREADY_EXISTS", ex.ErrorCode);
    }

    [Fact]
    public async Task CreateUserAsync_PasswordTooShort_ThrowsValidationException()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new AdminUserService(dbContext, NullLogger<AdminUserService>.Instance);

        var dto = new CreateAdminUserDto
        {
            Email = "valid@nexus.local",
            Password = "123", // Short password (< 8 chars)
            FirstName = "Van",
            LastName = "Nguyen"
        };

        var ex = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            service.CreateUserAsync(dto, null, CancellationToken.None));

        Assert.Equal("VAL_INVALID_PASSWORD", ex.ErrorCode);
    }

    [Fact]
    public async Task CreateUserAsync_MissingEmail_ThrowsValidationException()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new AdminUserService(dbContext, NullLogger<AdminUserService>.Instance);

        var dto = new CreateAdminUserDto
        {
            Email = "",
            Password = "Password123!",
            FirstName = "Van",
            LastName = "Nguyen"
        };

        var ex = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            service.CreateUserAsync(dto, null, CancellationToken.None));

        Assert.Equal("VAL_INVALID_EMAIL", ex.ErrorCode);
    }

    [Fact]
    public async Task CreateUserAsync_MissingFirstOrLastName_ThrowsValidationException()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new AdminUserService(dbContext, NullLogger<AdminUserService>.Instance);

        var dtoNoFirst = new CreateAdminUserDto
        {
            Email = "test@nexus.local",
            Password = "Password123!",
            FirstName = "",
            LastName = "Nguyen"
        };

        var ex1 = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            service.CreateUserAsync(dtoNoFirst, null, CancellationToken.None));
        Assert.Equal("VAL_INVALID_FIRST_NAME", ex1.ErrorCode);

        var dtoNoLast = new CreateAdminUserDto
        {
            Email = "test@nexus.local",
            Password = "Password123!",
            FirstName = "Van",
            LastName = ""
        };

        var ex2 = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            service.CreateUserAsync(dtoNoLast, null, CancellationToken.None));
        Assert.Equal("VAL_INVALID_LAST_NAME", ex2.ErrorCode);
    }

    [Fact]
    public async Task CreateUserAsync_MultipleRoles_AssignsAllRoles()
    {
        using var dbContext = CreateInMemoryDbContext();
        await SeedSystemRolesAsync(dbContext);
        var service = new AdminUserService(dbContext, NullLogger<AdminUserService>.Instance);

        var dto = new CreateAdminUserDto
        {
            Email = "dual.role@nexus.local",
            Password = "Password123!",
            FirstName = "Super",
            LastName = "Staff",
            RoleCodes = new List<string> { "staff", "admin" }
        };

        var response = await service.CreateUserAsync(dto, Guid.NewGuid(), CancellationToken.None);

        Assert.Contains("support staff", response.Roles);
        Assert.Contains("administrator", response.Roles);

        var userId = Guid.Parse(response.UserId);
        var userRoles = await dbContext.UserRoles.Where(ur => ur.UserId == userId).ToListAsync();
        Assert.Equal(2, userRoles.Count);
    }

    [Fact]
    public async Task GetUsersAsync_Pagination_CalculatesCorrectOffsetsAndTotals()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new AdminUserService(dbContext, NullLogger<AdminUserService>.Instance);

        for (int i = 1; i <= 5; i++)
        {
            dbContext.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                Email = $"user{i}@nexus.local",
                PasswordHash = "hash",
                FirstName = $"User{i}",
                LastName = "Test",
                Status = UserStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(i)
            });
        }
        await dbContext.SaveChangesAsync();

        // Page 1, Limit 2
        var page1 = await service.GetUsersAsync(page: 1, limit: 2, cancellationToken: CancellationToken.None);
        Assert.Equal(5, page1.Total);
        Assert.Equal(2, page1.Items.Count);
        Assert.Equal(1, page1.Page);
        Assert.Equal(2, page1.Limit);
        Assert.Equal("user5@nexus.local", page1.Items[0].Email); // Newest first

        // Page 3, Limit 2 (last page with 1 item remaining)
        var page3 = await service.GetUsersAsync(page: 3, limit: 2, cancellationToken: CancellationToken.None);
        Assert.Equal(5, page3.Total);
        Assert.Single(page3.Items);
        Assert.Equal(3, page3.Page);
    }

    [Fact]
    public async Task GetUsersAsync_KeywordSearch_MatchesEmailOrNameOrPhone()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new AdminUserService(dbContext, NullLogger<AdminUserService>.Instance);

        dbContext.Users.AddRange(
            new User
            {
                Id = Guid.NewGuid(),
                Email = "alex.rivers@nexus.local",
                PasswordHash = "hash",
                FirstName = "Alex",
                LastName = "Rivers",
                PhoneNumber = "+84901234567",
                Status = UserStatus.Active
            },
            new User
            {
                Id = Guid.NewGuid(),
                Email = "john.doe@nexus.local",
                PasswordHash = "hash",
                FirstName = "John",
                LastName = "Doe",
                PhoneNumber = "+84909876543",
                Status = UserStatus.Active
            }
        );
        await dbContext.SaveChangesAsync();

        // Search by keyword "alex"
        var searchAlex = await service.GetUsersAsync(keyword: "alex", cancellationToken: CancellationToken.None);
        Assert.Equal(1, searchAlex.Total);
        Assert.Single(searchAlex.Items);
        Assert.Equal("alex.rivers@nexus.local", searchAlex.Items[0].Email);

        // Search by phone "9876543"
        var searchPhone = await service.GetUsersAsync(keyword: "9876543", cancellationToken: CancellationToken.None);
        Assert.Equal(1, searchPhone.Total);
        Assert.Equal("john.doe@nexus.local", searchPhone.Items[0].Email);
    }

    [Fact]
    public async Task GetUsersAsync_RoleFilter_ReturnsOnlyMatchingRoleUsers()
    {
        using var dbContext = CreateInMemoryDbContext();
        await SeedSystemRolesAsync(dbContext);
        var service = new AdminUserService(dbContext, NullLogger<AdminUserService>.Instance);

        var staffRole = await dbContext.Roles.FirstAsync(r => r.RoleCode == RoleType.SupportStaff);
        var buyerRole = await dbContext.Roles.FirstAsync(r => r.RoleCode == RoleType.Buyer);

        var staffUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "staff.role@nexus.local",
            PasswordHash = "hash",
            FirstName = "Staff",
            LastName = "Person",
            Status = UserStatus.Active
        };
        var buyerUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "buyer.role@nexus.local",
            PasswordHash = "hash",
            FirstName = "Buyer",
            LastName = "Person",
            Status = UserStatus.Active
        };

        dbContext.Users.AddRange(staffUser, buyerUser);
        dbContext.UserRoles.AddRange(
            new UserRole { Id = Guid.NewGuid(), UserId = staffUser.Id, RoleId = staffRole.Id },
            new UserRole { Id = Guid.NewGuid(), UserId = buyerUser.Id, RoleId = buyerRole.Id }
        );
        await dbContext.SaveChangesAsync();

        var staffList = await service.GetUsersAsync(role: "staff", cancellationToken: CancellationToken.None);
        Assert.Equal(1, staffList.Total);
        Assert.Single(staffList.Items);
        Assert.Equal("staff.role@nexus.local", staffList.Items[0].Email);
    }

    [Fact]
    public async Task GetUsersAsync_StatusFilter_ReturnsOnlyMatchingStatusUsers()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new AdminUserService(dbContext, NullLogger<AdminUserService>.Instance);

        dbContext.Users.AddRange(
            new User
            {
                Id = Guid.NewGuid(),
                Email = "active@nexus.local",
                PasswordHash = "hash",
                FirstName = "Active",
                LastName = "User",
                Status = UserStatus.Active
            },
            new User
            {
                Id = Guid.NewGuid(),
                Email = "banned@nexus.local",
                PasswordHash = "hash",
                FirstName = "Banned",
                LastName = "User",
                Status = UserStatus.Banned
            }
        );
        await dbContext.SaveChangesAsync();

        var bannedResult = await service.GetUsersAsync(status: "banned", cancellationToken: CancellationToken.None);
        Assert.Equal(1, bannedResult.Total);
        Assert.Single(bannedResult.Items);
        Assert.Equal("banned@nexus.local", bannedResult.Items[0].Email);
    }

    [Fact]
    public async Task GetUserDetailAsync_WhenUserExists_ReturnsCompleteDetailWithRolesPermissionsReputation()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new AdminUserService(dbContext, NullLogger<AdminUserService>.Instance);

        var perm1 = new Permission { Id = Guid.NewGuid(), PermissionCode = "USER.VIEW", PermissionName = "View User", Category = "USER" };
        var perm2 = new Permission { Id = Guid.NewGuid(), PermissionCode = "ORDER.CREATE", PermissionName = "Create Order", Category = "ORDER" };
        dbContext.Permissions.AddRange(perm1, perm2);

        var role = new Role { Id = Guid.NewGuid(), RoleCode = RoleType.SupportStaff, RoleName = "Support Staff" };
        dbContext.Roles.Add(role);

        var rp1 = new RolePermission { Id = Guid.NewGuid(), RoleId = role.Id, PermissionId = perm1.Id };
        var rp2 = new RolePermission { Id = Guid.NewGuid(), RoleId = role.Id, PermissionId = perm2.Id };
        dbContext.RolePermissions.AddRange(rp1, rp2);

        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "staff.detail@nexus.local",
            PasswordHash = "hash",
            FirstName = "Thanh",
            LastName = "Vinh",
            Username = "vinhth",
            PhoneNumber = "+84912345678",
            IdentifyNumber = "079200000001",
            AvatarUrl = "https://cdn.nexus.local/avatars/vinhth.png",
            Gender = Gender.Male,
            DateOfBirth = new DateTimeOffset(2000, 1, 15, 0, 0, 0, TimeSpan.Zero),
            Address = "District 1, Ho Chi Minh City",
            Status = UserStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Users.Add(user);

        var userRole = new UserRole { Id = Guid.NewGuid(), UserId = userId, RoleId = role.Id };
        dbContext.UserRoles.Add(userRole);

        var repScore = new ReputationScore
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CurrentScore = 80,
            TrustLevel = TrustLevel.Good
        };
        dbContext.ReputationScores.Add(repScore);

        var repLog = new ReputationLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ChangeAmount = 5,
            Reason = ReputationChangeReason.OrderCompleted,
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.ReputationLogs.Add(repLog);

        await dbContext.SaveChangesAsync();

        var detail = await service.GetUserDetailAsync(userId, CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal(userId.ToString(), detail.Id);
        Assert.Equal("staff.detail@nexus.local", detail.Email);
        Assert.Equal("vinhth", detail.Username);
        Assert.Equal("Thanh Vinh", detail.DisplayName);
        Assert.Equal("+84912345678", detail.PhoneNumber);
        Assert.Equal("079200000001", detail.IdentifyNumber);
        Assert.Equal("Male", detail.Gender);
        Assert.Equal(80, detail.ReputationScore);
        Assert.Equal("GOOD", detail.TrustLevel);
        Assert.Contains("support staff", detail.Roles);
        Assert.Contains("user.view", detail.Permissions);
        Assert.Contains("order.create", detail.Permissions);
        Assert.Single(detail.ReputationLogs);
        Assert.Equal(5, detail.ReputationLogs[0].ChangeAmount);
    }

    [Fact]
    public async Task GetUserDetailAsync_WhenUserIsSeller_ReturnsSellerProfile()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new AdminUserService(dbContext, NullLogger<AdminUserService>.Instance);

        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "seller.tech@nexus.local",
            PasswordHash = "hash",
            FirstName = "Shop",
            LastName = "Tech",
            Status = UserStatus.Active
        };
        dbContext.Users.Add(user);

        var sellerProfile = new SellerProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ShopName = "Nexus Official Tech Store",
            Hotline = "19001234",
            BusinessEmail = "techstore@nexus.local",
            PickupAddress = "123 Le Loi, D1, HCMC",
            ProvinceId = 1,
            DistrictId = 2,
            WardCode = "10001",
            Status = SellerProfileStatus.Approved,
            ReviewedAt = DateTimeOffset.UtcNow
        };
        dbContext.SellerProfiles.Add(sellerProfile);

        await dbContext.SaveChangesAsync();

        var detail = await service.GetUserDetailAsync(userId, CancellationToken.None);

        Assert.NotNull(detail);
        Assert.True(detail.IsSeller);
        Assert.NotNull(detail.SellerProfile);
        Assert.Equal("Nexus Official Tech Store", detail.SellerProfile.ShopName);
        Assert.Equal("APPROVED", detail.SellerProfile.Status);
        Assert.Equal("19001234", detail.SellerProfile.Hotline);
    }

    [Fact]
    public async Task GetUserDetailAsync_WhenUserIsNotSeller_ReturnsNullSellerProfile()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new AdminUserService(dbContext, NullLogger<AdminUserService>.Instance);

        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "buyer.only@nexus.local",
            PasswordHash = "hash",
            FirstName = "Regular",
            LastName = "Buyer",
            Status = UserStatus.Active
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var detail = await service.GetUserDetailAsync(userId, CancellationToken.None);

        Assert.NotNull(detail);
        Assert.False(detail.IsSeller);
        Assert.Null(detail.SellerProfile);
    }

    [Fact]
    public async Task GetUserDetailAsync_WhenUserNotFound_ThrowsDomainExceptionWithUserNotFoundCode()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new AdminUserService(dbContext, NullLogger<AdminUserService>.Instance);

        var nonExistentUserId = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.GetUserDetailAsync(nonExistentUserId, CancellationToken.None));

        Assert.Equal("USER_NOT_FOUND", ex.ErrorCode);
        Assert.Equal("User account not found.", ex.Message);
    }

    [Fact]
    public async Task GetRolesAsync_ReturnsAllActiveRolesWithPermissionsCount()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new AdminUserService(dbContext, NullLogger<AdminUserService>.Instance);

        var r1 = new Role { Id = Guid.NewGuid(), RoleCode = RoleType.Buyer, RoleName = "Buyer", IsSystemRole = true, IsActive = true };
        var r2 = new Role { Id = Guid.NewGuid(), RoleCode = RoleType.Admin, RoleName = "Administrator", IsSystemRole = true, IsActive = true };
        dbContext.Roles.AddRange(r1, r2);

        var perm = new Permission { Id = Guid.NewGuid(), PermissionCode = "ROLE.VIEW", PermissionName = "View Roles", Category = "ROLE" };
        dbContext.Permissions.Add(perm);
        dbContext.RolePermissions.Add(new RolePermission { Id = Guid.NewGuid(), RoleId = r2.Id, PermissionId = perm.Id });

        await dbContext.SaveChangesAsync();

        var result = await service.GetRolesAsync(CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2, result.Roles.Count);
        var adminRole = result.Roles.FirstOrDefault(r => r.RoleCode == "Admin");
        Assert.NotNull(adminRole);
        Assert.Equal("Administrator", adminRole.RoleName);
        Assert.Equal(1, adminRole.PermissionsCount);
    }

    [Fact]
    public async Task AssignUserRolesAsync_WhenValidPayload_UpdatesUserRolesAndLogsAudit()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new AdminUserService(dbContext, NullLogger<AdminUserService>.Instance);

        var rBuyer = new Role { Id = Guid.NewGuid(), RoleCode = RoleType.Buyer, RoleName = "Buyer" };
        var rSeller = new Role { Id = Guid.NewGuid(), RoleCode = RoleType.Seller, RoleName = "Seller" };
        dbContext.Roles.AddRange(rBuyer, rSeller);

        var targetUserId = Guid.NewGuid();
        var user = new User
        {
            Id = targetUserId,
            Email = "promote.seller@nexus.local",
            PasswordHash = "hash",
            FirstName = "Target",
            LastName = "User",
            Status = UserStatus.Active
        };
        dbContext.Users.Add(user);
        var testUr = new UserRole { Id = Guid.NewGuid(), UserId = targetUserId, RoleId = rBuyer.Id };
        dbContext.UserRoles.Add(testUr);
        await dbContext.SaveChangesAsync();

        var dto = new AssignAdminUserRolesDto
        {
            RoleCodes = new List<string> { "seller" },
            Reason = "Approved seller registration"
        };
        var adminId = Guid.NewGuid();

        var result = await service.AssignUserRolesAsync(targetUserId, dto, adminId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(targetUserId.ToString(), result.UserId);
        Assert.Contains("seller", result.Roles);

        var auditLog = await dbContext.AuditLogs.FirstOrDefaultAsync(a => a.EntityId == targetUserId.ToString());
        Assert.NotNull(auditLog);
        Assert.Equal(AuditAction.RoleAssign, auditLog.Action);
    }

    [Fact]
    public async Task AssignUserRolesAsync_WhenBothAdminAndStaffRequested_ThrowsSodViolationException()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new AdminUserService(dbContext, NullLogger<AdminUserService>.Instance);

        var targetUserId = Guid.NewGuid();
        var user = new User
        {
            Id = targetUserId,
            Email = "sod.target@nexus.local",
            PasswordHash = "hash",
            FirstName = "Sod",
            LastName = "Violator",
            Status = UserStatus.Active
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var dto = new AssignAdminUserRolesDto
        {
            RoleCodes = new List<string> { "admin", "staff" },
            Reason = "Attempt illegal dual privilege"
        };

        var ex = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            service.AssignUserRolesAsync(targetUserId, dto, Guid.NewGuid(), CancellationToken.None));

        Assert.Equal("ERR_SOD_VIOLATION", ex.ErrorCode);
    }

    [Fact]
    public async Task AssignUserRolesAsync_WhenAttemptingToDemoteLastAdmin_ThrowsCannotDemoteLastAdminException()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new AdminUserService(dbContext, NullLogger<AdminUserService>.Instance);

        var rAdmin = new Role { Id = Guid.NewGuid(), RoleCode = RoleType.Admin, RoleName = "Administrator" };
        var rBuyer = new Role { Id = Guid.NewGuid(), RoleCode = RoleType.Buyer, RoleName = "Buyer" };
        dbContext.Roles.AddRange(rAdmin, rBuyer);

        var soleAdminId = Guid.NewGuid();
        var soleAdmin = new User
        {
            Id = soleAdminId,
            Email = "sole.admin@nexus.local",
            PasswordHash = "hash",
            FirstName = "Sole",
            LastName = "Admin",
            Status = UserStatus.Active
        };
        dbContext.Users.Add(soleAdmin);
        dbContext.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = soleAdminId, RoleId = rAdmin.Id });
        await dbContext.SaveChangesAsync();

        var dto = new AssignAdminUserRolesDto
        {
            RoleCodes = new List<string> { "buyer" },
            Reason = "Demoting sole admin"
        };

        var ex = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            service.AssignUserRolesAsync(soleAdminId, dto, Guid.NewGuid(), CancellationToken.None));

        Assert.Equal("ERR_CANNOT_DEMOTE_LAST_ADMIN", ex.ErrorCode);
    }

    [Fact]
    public async Task AssignUserRolesAsync_WhenTargetUserNotFound_ThrowsUserNotFoundException()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new AdminUserService(dbContext, NullLogger<AdminUserService>.Instance);

        var dto = new AssignAdminUserRolesDto
        {
            RoleCodes = new List<string> { "buyer" },
            Reason = "Assign to nonexistent"
        };

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.AssignUserRolesAsync(Guid.NewGuid(), dto, Guid.NewGuid(), CancellationToken.None));

        Assert.Equal("USER_NOT_FOUND", ex.ErrorCode);
    }
}
