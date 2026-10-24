namespace User.Application.Interfaces;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using User.Domain.Entities;

public interface IUserDbContext
{
    DbSet<User> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Role> Roles { get; }
    DbSet<UserRole> UserRoles { get; }
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
}
