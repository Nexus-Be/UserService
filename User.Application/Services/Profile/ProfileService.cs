namespace User.Application.Services.Profile;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using User.Application.Common.Extensions;
using User.Application.Common.Helpers;
using User.Application.Common.Models;
using User.Application.Interfaces;
using User.Domain.Exceptions;

public class ProfileService : IProfileService
{
    private readonly IUserDbContext _dbContext;
    private readonly ILogger<ProfileService> _logger;
    private readonly IFileStorageService? _fileStorageService;

    public ProfileService(
        IUserDbContext dbContext,
        ILogger<ProfileService> logger,
        IFileStorageService? fileStorageService = null)
    {
        _dbContext = dbContext;
        _logger = logger;
        _fileStorageService = fileStorageService;
    }

    // =========================================================================================
    // SECTION 1: QUẢN LÝ HỒ SƠ CÁ NHÂN VÀ HỒ SƠ CÔNG KHAI (PROFILE MANAGEMENT & PUBLIC QUERIES)
    // =========================================================================================

    /// <summary>
    /// Tra cứu thông tin hồ sơ cá nhân đầy đủ của tài khoản đang đăng nhập.
    /// Bao gồm Email, Số điện thoại, Họ tên, Vai trò hiện tại, và Hồ sơ gian hàng (nếu có).
    /// </summary>
    /// <param name="userId">ID người dùng đang đăng nhập.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu.</param>
    /// <returns>Hồ sơ cá nhân đầy đủ.</returns>
    public async Task<UserProfileDto> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "User identifier is required.");
        }

        var user = await _dbContext.Users
            .Include(u => u.Role)
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

    /// <summary>
    /// Tra cứu thông tin hồ sơ công khai của một người dùng bất kỳ.
    /// Ẩn hoàn toàn email cá nhân và số điện thoại để đảm bảo tính riêng tư và bảo mật.
    /// </summary>
    /// <param name="userId">ID người dùng cần tra cứu.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu.</param>
    /// <returns>Hồ sơ công khai rút gọn (Họ tên, Avatar, Bio, Rating).</returns>
    public async Task<PublicUserProfileDto> GetPublicProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "User identifier is required.");
        }

        var user = await _dbContext.Users
            .Include(u => u.Role)
            .Include(u => u.SellerProfile)
            .Include(u => u.ReputationScore)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null || user.IsDeleted)
        {
            _logger.LogWarning("[PUBLIC PROFILE] User not found or deleted for UserId: {UserId}", userId);
            throw new DomainException("USER_NOT_FOUND", "User was not found.", 404);
        }

        if (user.Status != Domain.Enums.UserStatus.Active)
        {
            throw new DomainException("USER_INACTIVE", "Account is not active or has been deleted.", 400);
        }

        var repScore = user.ReputationScore?.CurrentScore ?? 50;
        var trustLevel = user.ReputationScore?.TrustLevel.ToString() ?? "Standard";
        var isSeller = (user.SellerProfile != null && user.SellerProfile.Status == Domain.Enums.SellerProfileStatus.Approved)
            || user.Role?.RoleCode == Domain.Enums.RoleType.Seller;

        return new PublicUserProfileDto(
            Id: user.Id.ToString(),
            Username: user.Username,
            FirstName: user.FirstName,
            LastName: user.LastName,
            AvatarUrl: user.AvatarUrl,
            ReputationScore: repScore,
            TrustLevel: trustLevel,
            Badges: new List<string>(),
            IsSeller: isSeller,
            JoinedAt: user.CreatedAt);
    }

    /// <summary>
    /// Cập nhật thông tin hồ sơ cá nhân của chính người dùng đang đăng nhập.
    /// Cho phép sửa đổi Họ tên, Số điện thoại, Tiểu sử (Bio), và Ảnh đại diện (Avatar).
    /// </summary>
    /// <param name="userId">ID người dùng đang đăng nhập.</param>
    /// <param name="dto">Dữ liệu cập nhật.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu.</param>
    /// <returns>Hồ sơ cá nhân sau khi cập nhật thành công.</returns>
    public async Task<UserProfileDto> UpdateProfileAsync(Guid userId, UpdateProfileDto dto, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "User identifier is required.");
        }

        var user = await _dbContext.Users
            .Include(u => u.Role)
            .Include(u => u.SellerProfile)
            .Include(u => u.ReputationScore)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null || user.IsDeleted)
        {
            _logger.LogWarning("[PROFILE UPDATE] User not found or deleted for UserId: {UserId}", userId);
            throw new DomainException("USER_NOT_FOUND", "User account not found.");
        }

        var firstName = dto.FirstName;
        var lastName = dto.LastName;

        if (string.IsNullOrWhiteSpace(firstName) && !string.IsNullOrWhiteSpace(dto.DisplayName))
        {
            var parts = dto.DisplayName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            firstName = parts[0];
            if (parts.Length > 1 && string.IsNullOrWhiteSpace(lastName))
            {
                lastName = parts[1];
            }
        }

        // 1. Validate FirstName if provided
        if (firstName != null)
        {
            var trimmedFirst = firstName.Trim();
            if (string.IsNullOrWhiteSpace(trimmedFirst) || trimmedFirst.Length > 100)
            {
                throw new BusinessRuleValidationException("VAL_INVALID_FIRST_NAME", "First name must be between 1 and 100 characters.");
            }
            user.FirstName = trimmedFirst;
        }

        // 2. Validate LastName if provided
        if (lastName != null)
        {
            var trimmedLast = lastName.Trim();
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

        // 8. Username if provided
        if (!string.IsNullOrWhiteSpace(dto.Username))
        {
            var trimmedUsername = dto.Username.Trim();
            if (trimmedUsername.Length < 3 || trimmedUsername.Length > 50)
            {
                throw new BusinessRuleValidationException("VAL_INVALID_USERNAME", "Username must be between 3 and 50 characters.");
            }

            var usernameExists = await _dbContext.Users
                .AnyAsync(u => u.Id != userId && u.Username != null && u.Username.ToLower() == trimmedUsername.ToLower(), cancellationToken);

            if (usernameExists)
            {
                throw new DomainException("USERNAME_ALREADY_EXISTS", "Username is already taken by another account.", 409);
            }

            user.Username = trimmedUsername;
        }

        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[PROFILE UPDATE] Profile updated successfully for '{Email}' (UserId: {UserId})", user.Email, user.Id);
        return MapToDto(user);
    }

    private static UserProfileDto MapToDto(Domain.Entities.User user)
    {
        var roles = user.GetInheritedRoleClaims();

        var fullName = $"{user.FirstName} {user.LastName}".Trim();
        var displayName = !string.IsNullOrWhiteSpace(fullName)
            ? fullName
            : (!string.IsNullOrWhiteSpace(user.Username) ? user.Username : user.Email);

        var repScore = user.ReputationScore?.CurrentScore ?? 50;
        var trustLevel = user.ReputationScore?.TrustLevel.ToString() ?? "Standard";
        var isSeller = (user.SellerProfile != null && user.SellerProfile.Status == Domain.Enums.SellerProfileStatus.Approved)
            || user.Role?.RoleCode == Domain.Enums.RoleType.Seller;

        return new UserProfileDto(
            Id: user.Id.ToString(),
            Email: user.Email,
            Username: user.Username,
            FirstName: user.FirstName,
            LastName: user.LastName,
            DisplayName: displayName,
            PhoneNumber: user.PhoneNumber,
            Address: user.Address,
            AvatarUrl: user.AvatarUrl,
            Status: user.Status.ToString(),
            Roles: roles,
            Permissions: Array.Empty<string>(),
            IsSeller: isSeller,
            ShopName: user.SellerProfile?.ShopName ?? string.Empty,
            ReputationScore: repScore,
            TrustLevel: trustLevel,
            CreatedAt: user.CreatedAt);
    }

    public async Task<PagedData<ActiveSessionDto>> GetActiveSessionsAsync(
        Guid userId,
        Guid? currentSessionId,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "User identifier is required.");
        }

        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var now = DateTimeOffset.UtcNow;

        var baseQuery = _dbContext.RefreshTokens
            .AsNoTracking()
            .Where(t => t.UserId == userId && !t.IsRevoked && t.ExpiresAt > now);

        var query = baseQuery.Where(t => t.Id == baseQuery
            .Where(inner => (inner.SessionId != Guid.Empty ? inner.SessionId : inner.Id)
                         == (t.SessionId != Guid.Empty ? t.SessionId : t.Id))
            .OrderByDescending(inner => inner.UpdatedAt)
            .ThenByDescending(inner => inner.CreatedAt)
            .Select(inner => inner.Id)
            .FirstOrDefault());

        var totalCount = await query.CountAsync(cancellationToken);

        var tokens = await query
            .OrderByDescending(t => t.UpdatedAt)
            .ThenByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new
            {
                t.Id,
                t.SessionId,
                t.DeviceInfo,
                t.IpAddress,
                t.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        var items = tokens.Select(t =>
        {
            var effectiveSessionId = t.SessionId != Guid.Empty ? t.SessionId : t.Id;
            var isCurrent = currentSessionId.HasValue && effectiveSessionId == currentSessionId.Value;

            return new ActiveSessionDto(
                SessionId: effectiveSessionId,
                DeviceInfo: t.DeviceInfo,
                IpAddress: t.IpAddress,
                LastActiveAt: t.UpdatedAt,
                IsCurrent: isCurrent);
        }).ToList();

        return PagedData<ActiveSessionDto>.From(items, page, pageSize, totalCount);
    }

    public async Task RevokeSessionAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || sessionId == Guid.Empty)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "User identifier and session ID are required.");
        }

        var token = await _dbContext.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.SessionId == sessionId || t.Id == sessionId, cancellationToken);

        if (token == null)
        {
            throw new DomainException("SESSION_NOT_FOUND", "The specified session was not found.", 404);
        }

        if (token.UserId != userId)
        {
            throw new DomainException("FORBIDDEN", "You do not have permission to manage another user session.", 403);
        }

        var now = DateTimeOffset.UtcNow;
        var targetSessionId = token.SessionId != Guid.Empty ? token.SessionId : token.Id;

        if (token.SessionId != Guid.Empty)
        {
            await _dbContext.RevokeSessionTokensAsync(targetSessionId, now, cancellationToken);
        }
        else
        {
            await _dbContext.TryRevokeRefreshTokenAtomicAsync(token.Id, now, cancellationToken);
        }

        _logger.LogInformation("[SESSION] User '{UserId}' revoked session '{SessionId}'.", userId, targetSessionId);
    }

    public async Task<UploadAvatarResponse> UploadMyAvatarAsync(
        Guid userId,
        Stream content,
        string fileName,
        string contentType,
        long fileSize,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "User identifier is required.");
        }

        FileValidationHelper.ValidateImage(
            content,
            fileName,
            fileSize,
            maxSizeMb: 2,
            invalidTypeMessage: "Unsupported file format, only JPG, PNG, and WEBP are accepted.");

        if (_fileStorageService == null)
        {
            throw new DomainException("STORAGE_UPLOAD_FAILED", "File storage service is not configured.", 500);
        }

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, cancellationToken);

        if (user == null)
        {
            throw new DomainException("USER_NOT_FOUND", "User not found with the specified ID.", 404);
        }

        var stored = await _fileStorageService.UploadAsync(
            content,
            fileName,
            contentType,
            folder: "avatars",
            isPrivate: false,
            isVideo: false,
            cancellationToken: cancellationToken);

        var now = DateTimeOffset.UtcNow;
        user.AvatarUrl = stored.Url;
        user.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[PROFILE] Updated avatar for UserId '{UserId}'.", userId);

        return new UploadAvatarResponse(stored.Url, now);
    }

    public async Task<UserAvatarResponse> GetUserAvatarAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "User identifier is required.");
        }

        var user = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == userId && !u.IsDeleted)
            .Select(u => new { u.Id, u.AvatarUrl })
            .FirstOrDefaultAsync(cancellationToken);

        if (user == null)
        {
            throw new DomainException("USER_NOT_FOUND", "User not found with the specified ID.", 404);
        }

        return new UserAvatarResponse(user.Id, user.AvatarUrl);
    }
}
