namespace User.Application.Services.Profile;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using User.Application.Interfaces;
using User.Domain.Exceptions;

public class ProfileService : IProfileService
{
    private readonly IUserDbContext _dbContext;
    private readonly ILogger<ProfileService> _logger;

    public ProfileService(IUserDbContext dbContext, ILogger<ProfileService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<UserProfileDto> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "User identifier is required.");
        }

        var user = await _dbContext.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Include(u => u.SellerProfile)
            .Include(u => u.ReputationScore)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null || user.IsDeleted)
        {
            _logger.LogWarning("[PROFILE] User profile not found or deleted for UserId: {UserId}", userId);
            throw new DomainException("USER_NOT_FOUND", "User account not found.");
        }

        var roles = new List<string>();
        if (user.UserRoles != null && user.UserRoles.Count > 0)
        {
            foreach (var ur in user.UserRoles)
            {
                var roleName = ur.Role?.RoleName?.ToLowerInvariant() ?? ur.Role?.RoleCode.ToString().ToLowerInvariant();
                if (!string.IsNullOrWhiteSpace(roleName) && !roles.Contains(roleName))
                {
                    roles.Add(roleName);
                }
            }
        }

        if (roles.Count == 0)
        {
            roles.Add("buyer");
        }

        var fullName = $"{user.FirstName} {user.LastName}".Trim();
        var displayName = !string.IsNullOrWhiteSpace(fullName)
            ? fullName
            : (!string.IsNullOrWhiteSpace(user.Username) ? user.Username : user.Email);

        var repScore = user.ReputationScore?.CurrentScore ?? 50;
        var trustLevel = user.ReputationScore?.TrustLevel.ToString() ?? "Standard";

        _logger.LogInformation("[PROFILE] Profile retrieved for '{Email}' (UserId: {UserId})", user.Email, user.Id);

        return new UserProfileDto(
            Id: user.Id.ToString(),
            Username: user.Username ?? string.Empty,
            Email: user.Email,
            FirstName: user.FirstName,
            LastName: user.LastName,
            DisplayName: displayName,
            PhoneNumber: user.PhoneNumber,
            Address: user.Address,
            AvatarUrl: user.AvatarUrl,
            Status: user.Status.ToString(),
            Roles: roles,
            ShopName: user.SellerProfile?.ShopName ?? string.Empty,
            ReputationScore: repScore,
            TrustLevel: trustLevel);
    }
}
