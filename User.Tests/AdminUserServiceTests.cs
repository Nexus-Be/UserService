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
}
