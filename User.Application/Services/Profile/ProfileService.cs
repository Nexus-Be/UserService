namespace User.Application.Services.Profile;

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
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

        _logger.LogInformation("[PROFILE] Profile retrieved for '{Email}' (UserId: {UserId})", user.Email, user.Id);
        return MapToDto(user);
    }

    public async Task<UserProfileDto> UpdateProfileAsync(Guid userId, UpdateProfileDto dto, CancellationToken cancellationToken = default)
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
            _logger.LogWarning("[PROFILE UPDATE] User not found or deleted for UserId: {UserId}", userId);
            throw new DomainException("USER_NOT_FOUND", "User account not found.");
        }

        // 1. Validate FirstName if provided
        if (dto.FirstName != null)
        {
            var trimmedFirst = dto.FirstName.Trim();
            if (string.IsNullOrWhiteSpace(trimmedFirst) || trimmedFirst.Length > 100)
            {
                throw new BusinessRuleValidationException("VAL_INVALID_FIRST_NAME", "First name must be between 1 and 100 characters.");
            }
            user.FirstName = trimmedFirst;
        }

        // 2. Validate LastName if provided
        if (dto.LastName != null)
        {
            var trimmedLast = dto.LastName.Trim();
            if (string.IsNullOrWhiteSpace(trimmedLast) || trimmedLast.Length > 100)
            {
                throw new BusinessRuleValidationException("VAL_INVALID_LAST_NAME", "Last name must be between 1 and 100 characters.");
            }
            user.LastName = trimmedLast;
        }

        // 3. Validate PhoneNumber if provided
        if (dto.PhoneNumber != null)
        {
            var trimmedPhone = dto.PhoneNumber.Trim();
            if (!string.IsNullOrWhiteSpace(trimmedPhone))
            {
                if (trimmedPhone.Length > 20 || !Regex.IsMatch(trimmedPhone, @"^[0-9+() -]+$"))
                {
                    throw new BusinessRuleValidationException("VAL_INVALID_PHONE", "Phone number is invalid.");
                }

                // Check uniqueness across other users
                var phoneExists = await _dbContext.Users
                    .AnyAsync(u => u.Id != userId && u.PhoneNumber == trimmedPhone, cancellationToken);

                if (phoneExists)
                {
                    throw new DomainException("PHONE_ALREADY_EXISTS", "Phone number is already in use by another account.");
                }
            }
            user.PhoneNumber = string.IsNullOrWhiteSpace(trimmedPhone) ? null : trimmedPhone;
        }

        // 4. Validate Address if provided
        if (dto.Address != null)
        {
            var trimmedAddress = dto.Address.Trim();
            if (trimmedAddress.Length > 255)
            {
                throw new BusinessRuleValidationException("VAL_INVALID_ADDRESS", "Address cannot exceed 255 characters.");
            }
            user.Address = string.IsNullOrWhiteSpace(trimmedAddress) ? null : trimmedAddress;
        }

        // 5. AvatarUrl if provided
        if (dto.AvatarUrl != null)
        {
            user.AvatarUrl = string.IsNullOrWhiteSpace(dto.AvatarUrl.Trim()) ? null : dto.AvatarUrl.Trim();
        }

        // 6. Gender if provided
        if (dto.Gender.HasValue)
        {
            user.Gender = dto.Gender.Value;
        }

        // 7. DateOfBirth if provided
        if (dto.DateOfBirth.HasValue)
        {
            user.DateOfBirth = dto.DateOfBirth.Value;
        }

        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[PROFILE UPDATE] Profile updated successfully for '{Email}' (UserId: {UserId})", user.Email, user.Id);
        return MapToDto(user);
    }

    private static UserProfileDto MapToDto(Domain.Entities.User user)
    {
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
