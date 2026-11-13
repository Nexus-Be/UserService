namespace User.Infrastructure.Persistence;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using User.Application.Interfaces;
using User.Domain.Entities;

public class AppDbContext : DbContext, IUserDbContext
{
    private static readonly SemaphoreSlim InMemoryTokenLock = new(1, 1);

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Role> Roles => Set<Role>();

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
    public DbSet<UserAuctionProfile> UserAuctionProfiles => Set<UserAuctionProfile>();
    public DbSet<AuctionViolation> AuctionViolations => Set<AuctionViolation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        Seeds.UserSeedData.SeedRolesAndConfigs(modelBuilder);
    }

    public async Task<bool> TryRevokeRefreshTokenAtomicAsync(Guid tokenId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        if (Database.IsRelational())
        {
            var affected = await RefreshTokens
                .Where(t => t.Id == tokenId && !t.IsRevoked)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(t => t.IsRevoked, true)
                    .SetProperty(t => t.RevokedAt, now)
                    .SetProperty(t => t.UpdatedAt, now), cancellationToken);

            return affected == 1;
        }

        await InMemoryTokenLock.WaitAsync(cancellationToken);
        try
        {
            var token = await RefreshTokens
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tokenId, cancellationToken);
            if (token == null || token.IsRevoked)
            {
                return false;
            }

            var tracked = RefreshTokens.Local.FirstOrDefault(t => t.Id == tokenId);
            if (tracked != null)
            {
                tracked.IsRevoked = true;
                tracked.RevokedAt = now;
                tracked.UpdatedAt = now;
            }
            else
            {
                token.IsRevoked = true;
                token.RevokedAt = now;
                token.UpdatedAt = now;
                RefreshTokens.Update(token);
            }

            await SaveChangesAsync(cancellationToken);
            return true;
        }
        finally
        {
            InMemoryTokenLock.Release();
        }
    }

    public async Task<int> RevokeSessionTokensAsync(Guid sessionId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        if (sessionId == Guid.Empty)
        {
            return 0;
        }

        if (Database.IsRelational())
        {
            return await RefreshTokens
                .Where(t => t.SessionId == sessionId && !t.IsRevoked)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(t => t.IsRevoked, true)
                    .SetProperty(t => t.RevokedAt, now)
                    .SetProperty(t => t.UpdatedAt, now), cancellationToken);
        }

        var activeTokens = await RefreshTokens
            .Where(t => t.SessionId == sessionId && !t.IsRevoked)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = now;
            token.UpdatedAt = now;
        }

        if (activeTokens.Count > 0)
        {
            await SaveChangesAsync(cancellationToken);
        }

        return activeTokens.Count;
    }
}
