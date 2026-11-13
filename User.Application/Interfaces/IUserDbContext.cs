namespace User.Application.Interfaces;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using User.Domain.Entities;

public interface IUserDbContext
{
    DbSet<User> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Role> Roles { get; }

    DbSet<Permission> Permissions { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<SellerProfile> SellerProfiles { get; }
    DbSet<ReputationScore> ReputationScores { get; }
    DbSet<ReputationLog> ReputationLogs { get; }
    DbSet<UserReview> UserReviews { get; }
    DbSet<UserReport> UserReports { get; }
    DbSet<UserPenalty> UserPenalties { get; }
    DbSet<SystemConfig> SystemConfigs { get; }
    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically marks an active refresh token as revoked (WHERE Id == tokenId AND !IsRevoked).
    /// Returns true if this caller won the atomic transition; false if already revoked.
    /// </summary>
    Task<bool> TryRevokeRefreshTokenAtomicAsync(Guid tokenId, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes all active refresh tokens belonging to the specified sessionId.
    /// </summary>
    Task<int> RevokeSessionTokensAsync(Guid sessionId, DateTimeOffset now, CancellationToken cancellationToken = default);
}
