namespace User.Application.Common.Extensions;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using User.Application.Interfaces;
using User.Domain.Entities;
using User.Domain.Enums;

internal static class AuctionProfileExtensions
{
    public static async Task<UserAuctionProfile> GetOrCreateAuctionProfileAsync(
        this IUserDbContext dbContext,
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var profile = await dbContext.UserAuctionProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (profile is not null)
        {
            return profile;
        }

        profile = new UserAuctionProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ViolationStreak = 0,
            RestrictionType = AuctionRestrictionType.None,
            CreatedAt = now,
            UpdatedAt = now
        };
        dbContext.UserAuctionProfiles.Add(profile);
        return profile;
    }

    public static (bool IsRestricted, AuctionRestrictionType? Type, DateTimeOffset? Until, string? Reason) ResolveActiveRestriction(
        this UserAuctionProfile? profile,
        DateTimeOffset now)
    {
        if (profile is null || !profile.IsCurrentlyRestricted(now))
        {
            return (false, null, null, null);
        }

        return (true, profile.RestrictionType, profile.RestrictionUntil, profile.RestrictionReason);
    }
}
