namespace User.Infrastructure.Persistence;

using MassTransit;
using Microsoft.EntityFrameworkCore;
using User.Application.Interfaces;
using User.Domain.Entities;

public class AppDbContext : DbContext, IUserDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<SellerProfile> SellerProfiles => Set<SellerProfile>();
    public DbSet<ReputationScore> ReputationScores => Set<ReputationScore>();
    public DbSet<ReputationLog> ReputationLogs => Set<ReputationLog>();
    public DbSet<UserReview> UserReviews => Set<UserReview>();
    public DbSet<UserReport> UserReports => Set<UserReport>();
    public DbSet<UserPenalty> UserPenalties => Set<UserPenalty>();
    public DbSet<SystemConfig> SystemConfigs => Set<SystemConfig>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        Seeds.UserSeedData.SeedRolesAndConfigs(modelBuilder);

        // MassTransit Transactional Outbox entities
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}

public class UserDbContext : AppDbContext
{
    public UserDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }
}
