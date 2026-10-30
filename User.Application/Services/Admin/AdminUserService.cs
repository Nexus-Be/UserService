namespace User.Application.Services.Admin;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using User.Application.Common;
using User.Application.Common.Extensions;
using User.Application.Interfaces;
using User.Domain.Entities;
using User.Domain.Enums;
using User.Domain.Exceptions;

public sealed class AdminUserService : IAdminUserService
{
    private readonly IUserDbContext _dbContext;
    private readonly ILogger<AdminUserService> _logger;

    public AdminUserService(IUserDbContext dbContext, ILogger<AdminUserService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AdminUserCreatedResponseDto> CreateUserAsync(
        CreateAdminUserDto dto,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        if (dto == null)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "User creation payload is required.");
        }

        // 1. Validate email
        if (string.IsNullOrWhiteSpace(dto.Email))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_EMAIL", "Valid email address is required.");
        }

        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
        if (!normalizedEmail.Contains('@') || !normalizedEmail.Contains('.'))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_EMAIL", "Valid email address is required.");
        }

        var emailTaken = await _dbContext.Users
            .AnyAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (emailTaken)
        {
            _logger.LogWarning("[ADMIN USER CREATE] Email '{Email}' is already registered.", normalizedEmail);
            throw new DomainException("EMAIL_ALREADY_EXISTS", "Email is already registered in the system.");
        }

        // 2. Validate password (8 - 72 characters for BCrypt)
        if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 8)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_PASSWORD", "Password must be at least 8 characters.");
        }

        if (dto.Password.Length > 72)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_PASSWORD", "Password cannot exceed 72 characters.");
        }

        // 3. Validate first and last name
        if (string.IsNullOrWhiteSpace(dto.FirstName))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_FIRST_NAME", "First name is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.LastName))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_LAST_NAME", "Last name is required.");
        }

        // 4. Validate username if provided
        string? username = null;
        if (!string.IsNullOrWhiteSpace(dto.Username))
        {
            username = dto.Username.Trim();
            if (username.Length < 3 || username.Length > 50)
            {
                throw new BusinessRuleValidationException("VAL_INVALID_USERNAME", "Username must be between 3 and 50 characters.");
            }

            var usernameLower = username.ToLowerInvariant();
            var usernameTaken = await _dbContext.Users
                .AnyAsync(u => u.Username != null && u.Username.ToLower() == usernameLower, cancellationToken);

            if (usernameTaken)
            {
                _logger.LogWarning("[ADMIN USER CREATE] Username '{Username}' is already taken.", username);
                throw new DomainException("USERNAME_ALREADY_EXISTS", "Username is already registered in the system.");
            }
        }

        // 5. Resolve role list (RoleCodes)
        var requestedRoles = new HashSet<RoleType>();
        if (dto.RoleCodes != null && dto.RoleCodes.Count > 0)
        {
            foreach (var code in dto.RoleCodes)
            {
                if (string.IsNullOrWhiteSpace(code)) continue;

                var normalizedCode = code.Trim().ToLowerInvariant().Replace("_", "").Replace("-", "");
                switch (normalizedCode)
                {
                    case "staff":
                    case "supportstaff":
                    case "support":
                        requestedRoles.Add(RoleType.SupportStaff);
                        break;
                    case "admin":
                    case "administrator":
                        requestedRoles.Add(RoleType.Admin);
                        break;
                    case "seller":
                        requestedRoles.Add(RoleType.Seller);
                        break;
                    case "buyer":
                    default:
                        requestedRoles.Add(RoleType.Buyer);
                        break;
                }
            }
        }

        // If no role specified, default to SupportStaff
        if (requestedRoles.Count == 0)
        {
            requestedRoles.Add(RoleType.SupportStaff);
        }

        // 6. Hash password with BCrypt
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

        // 7. Create new User entity
        var newUser = new Domain.Entities.User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            PasswordHash = passwordHash,
            FirstName = dto.FirstName.Trim(),
            LastName = dto.LastName.Trim(),
            PhoneNumber = !string.IsNullOrWhiteSpace(dto.PhoneNumber) ? dto.PhoneNumber.Trim() : null,
            Username = username,
            Status = UserStatus.Active,
            EmailVerifiedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            IsDeleted = false
        };

        _dbContext.Users.Add(newUser);

        // 8. Assign roles in UserRoles table
        var assignedRoleNames = new List<string>();
        foreach (var roleType in requestedRoles)
        {
            var roleEntity = await _dbContext.Roles
                .FirstOrDefaultAsync(r => r.RoleCode == roleType, cancellationToken);

            if (roleEntity == null)
            {
                // Fallback if role is not seeded in DB
                roleEntity = new Role
                {
                    Id = Guid.NewGuid(),
                    RoleCode = roleType,
                    RoleName = roleType.ToString(),
                    Description = $"System {roleType} role",
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                _dbContext.Roles.Add(roleEntity);
            }

            _dbContext.UserRoles.Add(new UserRole
            {
                Id = Guid.NewGuid(),
                UserId = newUser.Id,
                RoleId = roleEntity.Id,
                AssignedAt = DateTimeOffset.UtcNow,
                AssignedBy = adminId
            });

            assignedRoleNames.Add(roleEntity.RoleName.ToLowerInvariant());
        }

        // 9. Initialize default reputation score (50 - Standard)
        _dbContext.ReputationScores.Add(new ReputationScore
        {
            Id = Guid.NewGuid(),
            UserId = newUser.Id,
            CurrentScore = 50,
            TrustLevel = TrustLevel.Standard,
            TotalPositiveReviews = 0,
            TotalNegativeReviews = 0,
            PenaltyCount = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        // 10. Record audit log
        _dbContext.AuditLogs.Add(new AuditLog
        {
            ServiceName = "UserService",
            Action = AuditAction.RoleAssign,
            EntityName = nameof(Domain.Entities.User),
            EntityId = newUser.Id.ToString(),
            UserId = adminId?.ToString() ?? "SYSTEM_ADMIN",
            UserName = adminId?.ToString() ?? "SystemAdmin",
            NewValues = JsonSerializer.Serialize(new
            {
                Email = newUser.Email,
                FirstName = newUser.FirstName,
                LastName = newUser.LastName,
                Roles = assignedRoleNames,
                Status = newUser.Status.ToString(),
                CreatedBy = adminId
            }),
            CreatedAt = DateTimeOffset.UtcNow
        });

        // 11. Commit to database
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "[ADMIN USER CREATE] Successfully created user '{UserId}' with Email '{Email}' and Roles [{Roles}] by Admin '{AdminId}'.",
            newUser.Id, newUser.Email, string.Join(", ", assignedRoleNames), adminId);

        return new AdminUserCreatedResponseDto(
            UserId: newUser.Id.ToString(),
            Email: newUser.Email,
            Roles: assignedRoleNames,
            Status: newUser.Status.ToString(),
            CreatedAt: newUser.CreatedAt
        );
    }

    public async Task<AdminUserListPagedResultDto> GetUsersAsync(
        int page = 1,
        int limit = 20,
        string? keyword = null,
        string? role = null,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        var (safePage, safeLimit) = PaginationConstants.Normalize(page, limit);

        var query = _dbContext.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Include(u => u.ReputationScore)
            .Include(u => u.SellerProfile)
            .AsQueryable();

        // 1. Keyword search
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var trimmedKeyword = keyword.Trim().ToLower();
            query = query.Where(u =>
                u.Email.ToLower().Contains(trimmedKeyword) ||
                u.FirstName.ToLower().Contains(trimmedKeyword) ||
                u.LastName.ToLower().Contains(trimmedKeyword) ||
                (u.Username != null && u.Username.ToLower().Contains(trimmedKeyword)) ||
                (u.PhoneNumber != null && u.PhoneNumber.Contains(trimmedKeyword)));
        }

        // 2. Status filter
        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
        {
            if (status.TryToEnum<UserStatus>(out var parsedStatus))
            {
                query = query.Where(u => u.Status == parsedStatus);
            }
            else if (string.Equals(status.Trim(), "pending", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(u => u.Status == UserStatus.PendingVerification);
            }
        }

        // 3. Role filter
        if (!string.IsNullOrWhiteSpace(role) && !string.Equals(role, "all", StringComparison.OrdinalIgnoreCase))
        {
            var normalizedRole = role.Trim().ToLowerInvariant().Replace("_", "").Replace("-", "");
            RoleType? targetRoleType = normalizedRole switch
            {
                "staff" or "supportstaff" or "support" => RoleType.SupportStaff,
                "admin" or "administrator" => RoleType.Admin,
                "seller" => RoleType.Seller,
                "buyer" => RoleType.Buyer,
                _ => null
            };

            if (targetRoleType.HasValue)
            {
                query = query.Where(u => u.UserRoles.Any(ur => ur.Role != null && ur.Role.RoleCode == targetRoleType.Value));
            }
        }

        // 4. Count total records
        var total = await query.CountAsync(cancellationToken);

        // 5. Apply pagination (Skip & Take)
        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((safePage - 1) * safeLimit)
            .Take(safeLimit)
            .ToListAsync(cancellationToken);

        // 6. Map to DTOs
        var items = users.Select(u =>
        {
            var roles = u.UserRoles?
                .Where(ur => ur.Role != null)
                .Select(ur => ur.Role!.RoleName.ToLowerInvariant())
                .ToList() ?? new List<string>();

            if (roles.Count == 0)
            {
                roles.Add("buyer");
            }

            var displayName = $"{u.FirstName} {u.LastName}".Trim();
            var repScore = u.ReputationScore?.CurrentScore ?? 50;
            var trustLevel = (u.ReputationScore?.TrustLevel ?? TrustLevel.Standard).ToString().ToUpperInvariant();
            var isSeller = roles.Contains("seller") || u.SellerProfile != null;

            return new AdminUserListItemDto(
                Id: u.Id.ToString(),
                UserId: u.Id.ToString(),
                Username: u.Username,
                Email: u.Email,
                DisplayName: displayName,
                FirstName: u.FirstName,
                LastName: u.LastName,
                PhoneNumber: u.PhoneNumber,
                AvatarUrl: u.AvatarUrl,
                Address: u.Address,
                ReputationScore: repScore,
                TrustLevel: trustLevel,
                IsSeller: isSeller,
                Roles: roles,
                Status: u.Status.ToString().ToUpperInvariant(),
                CreatedAt: u.CreatedAt
            );
        }).ToList();

        return new AdminUserListPagedResultDto(items, total, safePage, safeLimit);
    }

    public async Task<AdminUserDetailDto> GetUserDetailAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[AdminUserService.GetUserDetailAsync] Fetching user profile detail for UserId: {UserId}", userId);

        var user = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role!)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .Include(u => u.SellerProfile)
            .Include(u => u.ReputationScore)
            .Include(u => u.ReputationLogs)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null)
        {
            _logger.LogWarning("[AdminUserService.GetUserDetailAsync] User not found with UserId: {UserId}", userId);
            throw new DomainException("USER_NOT_FOUND", "User account not found.");
        }

        // Roles
        var roles = user.UserRoles?
            .Where(ur => ur.Role != null)
            .Select(ur => ur.Role!.RoleName.ToLowerInvariant())
            .Distinct()
            .ToList() ?? new List<string>();

        if (roles.Count == 0)
        {
            roles.Add("buyer");
        }

        // Permissions
        var permissions = user.UserRoles?
            .Where(ur => ur.Role != null && ur.Role.RolePermissions != null)
            .SelectMany(ur => ur.Role!.RolePermissions)
            .Where(rp => rp.Permission != null)
            .Select(rp => rp.Permission!.PermissionCode.ToLowerInvariant())
            .Distinct()
            .OrderBy(p => p)
            .ToList() ?? new List<string>();

        // Reputation
        var repScore = user.ReputationScore?.CurrentScore ?? 50;
        var trustLevel = (user.ReputationScore?.TrustLevel ?? TrustLevel.Standard).ToString().ToUpperInvariant();

        // Seller Profile
        AdminSellerProfileDetailDto? sellerProfileDto = null;
        var isSeller = roles.Contains("seller") || (user.SellerProfile != null && user.SellerProfile.Status == SellerProfileStatus.Approved);

        if (user.SellerProfile != null)
        {
            var sp = user.SellerProfile;
            sellerProfileDto = new AdminSellerProfileDetailDto(
                ShopName: sp.ShopName,
                Status: sp.Status.ToString().ToUpperInvariant(),
                Hotline: sp.Hotline,
                BusinessEmail: sp.BusinessEmail,
                PickupAddress: sp.PickupAddress,
                ProvinceId: sp.ProvinceId,
                DistrictId: sp.DistrictId,
                WardCode: sp.WardCode,
                AvatarUrl: sp.AvatarUrl,
                IdCardFrontUrl: sp.IdCardFrontUrl,
                IdCardBackUrl: sp.IdCardBackUrl,
                BusinessLicenseUrl: sp.BusinessLicenseUrl,
                RejectionReason: sp.RejectionReason,
                ReviewedAt: sp.ReviewedAt
            );
        }

        // Reputation Logs (Top 10 recent logs)
        var reputationLogs = user.ReputationLogs?
            .OrderByDescending(l => l.CreatedAt)
            .Take(10)
            .Select(l => new AdminUserReputationLogDto(
                Id: l.Id.ToString(),
                ChangeAmount: l.ChangeAmount,
                Reason: l.Reason.ToString(),
                ReferenceType: l.ReferenceType?.ToString(),
                ReferenceId: l.ReferenceId,
                CreatedAt: l.CreatedAt
            ))
            .ToList() ?? new List<AdminUserReputationLogDto>();

        var displayName = $"{user.FirstName} {user.LastName}".Trim();

        return new AdminUserDetailDto(
            Id: user.Id.ToString(),
            UserId: user.Id.ToString(),
            Email: user.Email,
            Username: user.Username,
            DisplayName: displayName,
            FirstName: user.FirstName,
            LastName: user.LastName,
            PhoneNumber: user.PhoneNumber,
            IdentifyNumber: user.IdentifyNumber,
            AvatarUrl: user.AvatarUrl,
            Gender: user.Gender.ToString(),
            DateOfBirth: user.DateOfBirth,
            Address: user.Address,
            Status: user.Status.ToString().ToUpperInvariant(),
            IsSeller: isSeller,
            ReputationScore: repScore,
            TrustLevel: trustLevel,
            Roles: roles,
            Permissions: permissions,
            SellerProfile: sellerProfileDto,
            ReputationLogs: reputationLogs,
            BannedAt: user.BannedAt,
            BanReason: user.BanReason,
            EmailVerifiedAt: user.EmailVerifiedAt,
            CreatedAt: user.CreatedAt,
            UpdatedAt: user.UpdatedAt
        );
    }

    public async Task<AdminRoleListResponseDto> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        var roles = await _dbContext.Roles
            .AsNoTracking()
            .Where(r => !r.IsDeleted)
            .Include(r => r.RolePermissions)
            .OrderBy(r => r.RoleCode)
            .ToListAsync(cancellationToken);

        var dtoList = roles.Select(r => new AdminRoleDto(
            Id: r.Id.ToString(),
            RoleId: r.Id.ToString(),
            RoleCode: r.RoleCode.ToString(),
            RoleName: r.RoleName,
            Description: r.Description,
            IsSystemRole: r.IsSystemRole,
            IsActive: r.IsActive,
            PermissionsCount: r.RolePermissions?.Count(rp => !rp.IsDeleted) ?? 0,
            CreatedAt: r.CreatedAt
        )).ToList();

        return new AdminRoleListResponseDto(dtoList);
    }

    public async Task<AdminUserRolesResponseDto> AssignUserRolesAsync(
        Guid targetUserId,
        AssignAdminUserRolesDto dto,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        if (targetUserId == Guid.Empty)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Target user ID is required.");
        }

        if (dto == null)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Role assignment payload is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.Reason))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Reason for role assignment is required.");
        }

        // 1. Validate user exists
        var targetUser = await _dbContext.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == targetUserId && !u.IsDeleted, cancellationToken);

        if (targetUser == null)
        {
            throw new DomainException("USER_NOT_FOUND", "User was not found in the system.");
        }

        // 2. Resolve requested role list
        var requestedRoles = new HashSet<RoleType>();
        if (dto.RoleCodes != null && dto.RoleCodes.Count > 0)
        {
            foreach (var code in dto.RoleCodes)
            {
                if (string.IsNullOrWhiteSpace(code)) continue;
                var normalizedCode = code.Trim().ToLowerInvariant().Replace("_", "").Replace("-", "");
                switch (normalizedCode)
                {
                    case "admin":
                    case "administrator":
                        requestedRoles.Add(RoleType.Admin);
                        break;
                    case "staff":
                    case "supportstaff":
                    case "support":
                        requestedRoles.Add(RoleType.SupportStaff);
                        break;
                    case "seller":
                        requestedRoles.Add(RoleType.Seller);
                        break;
                    case "buyer":
                        requestedRoles.Add(RoleType.Buyer);
                        break;
                }
            }
        }

        // Handle RoleIds if passed as int or string
        if (dto.RoleIds != null && dto.RoleIds.Count > 0)
        {
            foreach (var rId in dto.RoleIds)
            {
                if (int.TryParse(rId, out var parsedInt) && Enum.IsDefined(typeof(RoleType), parsedInt))
                {
                    requestedRoles.Add((RoleType)parsedInt);
                }
            }
        }

        // If no role passed, default to Buyer
        if (requestedRoles.Count == 0)
        {
            requestedRoles.Add(RoleType.Buyer);
        }

        // 3. Segregation of Duties (SoD): Cannot combine Admin and SupportStaff
        if (requestedRoles.Contains(RoleType.Admin) && requestedRoles.Contains(RoleType.SupportStaff))
        {
            throw new BusinessRuleValidationException("ERR_SOD_VIOLATION", "A user cannot be assigned both Admin and SupportStaff roles simultaneously.");
        }

        // 4. Demotion Guard: Cannot demote the last active Administrator
        var currentRoles = targetUser.UserRoles
            .Where(ur => !ur.IsDeleted && ur.Role != null)
            .Select(ur => ur.Role!.RoleCode)
            .ToHashSet();

        if (currentRoles.Contains(RoleType.Admin) && !requestedRoles.Contains(RoleType.Admin))
        {
            var activeAdminCount = await _dbContext.UserRoles
                .Include(ur => ur.User)
                .Include(ur => ur.Role)
                .Where(ur => !ur.IsDeleted && ur.User != null && !ur.User.IsDeleted && ur.User.Status == UserStatus.Active && ur.Role != null && ur.Role.RoleCode == RoleType.Admin)
                .Select(ur => ur.UserId)
                .Distinct()
                .CountAsync(cancellationToken);

            if (activeAdminCount <= 1)
            {
                throw new BusinessRuleValidationException("ERR_CANNOT_DEMOTE_LAST_ADMIN", "Cannot remove Admin role from the last active Administrator in the system.");
            }
        }

        // 5. Update UserRoles
        var allDbRoles = await _dbContext.Roles
            .Where(r => !r.IsDeleted)
            .ToListAsync(cancellationToken);

        var oldRoleNames = targetUser.UserRoles
            .Where(ur => !ur.IsDeleted && ur.Role != null)
            .Select(ur => ur.Role!.RoleCode.ToString())
            .ToList();

        // Mark removed roles as deleted
        foreach (var existingUr in targetUser.UserRoles.Where(ur => !ur.IsDeleted).ToList())
        {
            if (existingUr.Role == null || !requestedRoles.Contains(existingUr.Role.RoleCode))
            {
                existingUr.IsDeleted = true;
                existingUr.DeletedAt = DateTimeOffset.UtcNow;
                existingUr.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        // Add new roles
        foreach (var reqRole in requestedRoles)
        {
            var alreadyHas = targetUser.UserRoles.Any(ur => !ur.IsDeleted && ur.Role?.RoleCode == reqRole);
            if (!alreadyHas)
            {
                var dbRole = allDbRoles.FirstOrDefault(r => r.RoleCode == reqRole);
                if (dbRole != null)
                {
                    _dbContext.UserRoles.Add(new UserRole
                    {
                        Id = Guid.NewGuid(),
                        UserId = targetUser.Id,
                        RoleId = dbRole.Id,
                        AssignedAt = DateTimeOffset.UtcNow,
                        AssignedBy = adminId ?? Guid.Empty,
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow,
                        IsDeleted = false
                    });
                }
            }
        }

        var newRoleNames = requestedRoles.Select(r => r.ToString().ToLowerInvariant()).ToList();

        // 6. Ghi AuditLog
        _dbContext.AuditLogs.Add(new AuditLog
        {
            ServiceName = "user-service",
            Action = AuditAction.RoleAssign,
            EntityName = "UserRoles",
            EntityId = targetUserId.ToString(),
            UserId = adminId?.ToString(),
            OldValues = JsonSerializer.Serialize(oldRoleNames),
            NewValues = JsonSerializer.Serialize(newRoleNames),
            ChangedColumns = JsonSerializer.Serialize(new[] { "UserRoles" }),
            CreatedAt = DateTimeOffset.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[ADMIN ASSIGN ROLES] Successfully updated roles for User '{UserId}'. New roles: {Roles}",
            targetUserId, string.Join(", ", newRoleNames));

        return new AdminUserRolesResponseDto(
            UserId: targetUserId.ToString(),
            Roles: newRoleNames,
            UpdatedAt: DateTimeOffset.UtcNow
        );
    }

    public async Task<AdminUserUpdatedResponseDto> UpdateUserAsync(
        Guid targetUserId,
        UpdateAdminUserDto dto,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        if (targetUserId == Guid.Empty)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Target user ID is required.");
        }

        if (dto == null)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "User update payload is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.Reason))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Reason for updating user is required.");
        }

        // 1. Find user in DB (including soft-deleted for restoration)
        var user = await _dbContext.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == targetUserId, cancellationToken);

        if (user == null)
        {
            _logger.LogWarning("[ADMIN USER UPDATE] User '{UserId}' not found.", targetUserId);
            throw new DomainException("USER_NOT_FOUND", "User was not found in the system.");
        }

        // 2. Collect OldValues
        var oldValues = new Dictionary<string, object?>
        {
            ["FirstName"] = user.FirstName,
            ["LastName"] = user.LastName,
            ["PhoneNumber"] = user.PhoneNumber,
            ["Status"] = user.Status.ToString().ToUpperInvariant()
        };

        var changedColumns = new List<string>();

        // 3. Update name fields
        if (dto.FirstName != null)
        {
            var trimmedFirst = dto.FirstName.Trim();
            if (string.IsNullOrWhiteSpace(trimmedFirst))
            {
                throw new BusinessRuleValidationException("VAL_INVALID_FIRST_NAME", "First name cannot be empty.");
            }
            if (trimmedFirst != user.FirstName)
            {
                user.FirstName = trimmedFirst;
                changedColumns.Add("FirstName");
            }
        }

        if (dto.LastName != null)
        {
            var trimmedLast = dto.LastName.Trim();
            if (string.IsNullOrWhiteSpace(trimmedLast))
            {
                throw new BusinessRuleValidationException("VAL_INVALID_LAST_NAME", "Last name cannot be empty.");
            }
            if (trimmedLast != user.LastName)
            {
                user.LastName = trimmedLast;
                changedColumns.Add("LastName");
            }
        }

        // 4. Update phone number
        if (dto.PhoneNumber != null)
        {
            var trimmedPhone = dto.PhoneNumber.Trim();
            if (!string.IsNullOrEmpty(trimmedPhone) && (trimmedPhone.Length < 7 || trimmedPhone.Length > 20))
            {
                throw new BusinessRuleValidationException("VAL_INVALID_PHONE", "Phone number must be between 7 and 20 characters.");
            }
            var newPhoneVal = string.IsNullOrEmpty(trimmedPhone) ? null : trimmedPhone;
            if (newPhoneVal != user.PhoneNumber)
            {
                user.PhoneNumber = newPhoneVal;
                changedColumns.Add("PhoneNumber");
            }
        }

        // 5. Update Status
        if (!string.IsNullOrWhiteSpace(dto.Status))
        {
            var normalizedStatus = dto.Status.Trim().ToLowerInvariant();
            UserStatus parsedStatus;
            switch (normalizedStatus)
            {
                case "active":
                    parsedStatus = UserStatus.Active;
                    break;
                case "banned":
                    parsedStatus = UserStatus.Banned;
                    break;
                case "pendingverification":
                case "pending_verification":
                case "pending":
                    parsedStatus = UserStatus.PendingVerification;
                    break;
                case "deleted":
                    parsedStatus = UserStatus.Deleted;
                    break;
                default:
                    throw new BusinessRuleValidationException("VAL_INVALID_USER_STATUS", "Invalid user status.");
            }

            if (parsedStatus != user.Status)
            {
                var previousStatus = user.Status;
                user.Status = parsedStatus;
                changedColumns.Add("Status");

                // If transitioning to Banned
                if (parsedStatus == UserStatus.Banned)
                {
                    user.BannedAt = DateTimeOffset.UtcNow;
                    user.BannedBy = adminId;
                    user.BanReason = dto.Reason.Trim();
                    changedColumns.Add("BannedAt");
                    changedColumns.Add("BannedBy");
                    changedColumns.Add("BanReason");
                }
                // If unbanning
                else if (previousStatus == UserStatus.Banned)
                {
                    user.BannedAt = null;
                    user.BannedBy = null;
                    user.BanReason = null;
                    changedColumns.Add("BannedAt");
                    changedColumns.Add("BannedBy");
                    changedColumns.Add("BanReason");
                }

                // If transitioning to Deleted
                if (parsedStatus == UserStatus.Deleted)
                {
                    user.IsDeleted = true;
                    user.DeletedAt = DateTimeOffset.UtcNow;
                    changedColumns.Add("IsDeleted");
                    changedColumns.Add("DeletedAt");
                }
                else if (previousStatus == UserStatus.Deleted)
                {
                    user.IsDeleted = false;
                    user.DeletedAt = null;
                    changedColumns.Add("IsDeleted");
                    changedColumns.Add("DeletedAt");
                }
            }
        }

        // 6. Update audit metadata
        user.UpdatedAt = DateTimeOffset.UtcNow;
        if (adminId.HasValue)
        {
            user.UpdatedBy = adminId.Value;
        }

        // Collect NewValues
        var newValues = new Dictionary<string, object?>
        {
            ["FirstName"] = user.FirstName,
            ["LastName"] = user.LastName,
            ["PhoneNumber"] = user.PhoneNumber,
            ["Status"] = user.Status.ToString().ToUpperInvariant(),
            ["Reason"] = dto.Reason.Trim()
        };

        // 7. Ghi AuditLog
        _dbContext.AuditLogs.Add(new AuditLog
        {
            ServiceName = "user-service",
            Action = AuditAction.UserUpdate,
            EntityName = "User",
            EntityId = targetUserId.ToString(),
            UserId = adminId?.ToString(),
            OldValues = JsonSerializer.Serialize(oldValues),
            NewValues = JsonSerializer.Serialize(newValues),
            ChangedColumns = JsonSerializer.Serialize(changedColumns),
            CreatedAt = DateTimeOffset.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[ADMIN USER UPDATE] Successfully updated user '{UserId}'. Changed columns: {ChangedCols}",
            targetUserId, string.Join(", ", changedColumns));

        return new AdminUserUpdatedResponseDto(
            Id: user.Id.ToString(),
            UserId: user.Id.ToString(),
            FirstName: user.FirstName,
            LastName: user.LastName,
            PhoneNumber: user.PhoneNumber,
            Status: user.Status.ToString().ToUpperInvariant(),
            UpdatedBy: adminId?.ToString() ?? "system",
            UpdatedAt: user.UpdatedAt,
            Message: "User updated successfully."
        );
    }

    public async Task DeleteUserAsync(
        Guid targetUserId,
        DeleteAdminUserDto dto,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        if (targetUserId == Guid.Empty)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Target user ID is required.");
        }

        if (dto == null || string.IsNullOrWhiteSpace(dto.Reason))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Reason for deleting user is required.");
        }

        // 1. Find user in DB (including query filter scope)
        var user = await _dbContext.Users
            .IgnoreQueryFilters()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == targetUserId, cancellationToken);

        if (user == null)
        {
            _logger.LogWarning("[ADMIN USER DELETE] User '{UserId}' not found.", targetUserId);
            throw new DomainException("USER_NOT_FOUND", "User was not found in the system.");
        }

        if (user.IsDeleted)
        {
            _logger.LogWarning("[ADMIN USER DELETE] User '{UserId}' has already been deleted.", targetUserId);
            throw new DomainException("USER_ALREADY_DELETED", "User has already been deleted.");
        }

        // 2. Demotion/Delete Guard for last active Admin
        var isTargetAdmin = user.UserRoles
            .Any(ur => !ur.IsDeleted && ur.Role != null && ur.Role.RoleCode == RoleType.Admin);

        if (isTargetAdmin)
        {
            var activeAdminCount = await _dbContext.UserRoles
                .Include(ur => ur.User)
                .Include(ur => ur.Role)
                .Where(ur => !ur.IsDeleted && ur.User != null && !ur.User.IsDeleted && ur.User.Status == UserStatus.Active && ur.Role != null && ur.Role.RoleCode == RoleType.Admin)
                .Select(ur => ur.UserId)
                .Distinct()
                .CountAsync(cancellationToken);

            if (activeAdminCount <= 1)
            {
                throw new BusinessRuleValidationException("ERR_CANNOT_DELETE_LAST_ADMIN", "Cannot delete the last active Administrator in the system.");
            }
        }

        var now = DateTimeOffset.UtcNow;
        var oldValues = new Dictionary<string, object?>
        {
            ["IsDeleted"] = user.IsDeleted,
            ["Status"] = user.Status.ToString().ToUpperInvariant()
        };

        // 3. Perform soft delete
        user.IsDeleted = true;
        user.DeletedAt = now;
        user.Status = UserStatus.Deleted;
        user.UpdatedAt = now;
        if (adminId.HasValue)
        {
            user.UpdatedBy = adminId.Value;
        }

        // 4. Revoke all active refresh tokens
        var activeTokens = await _dbContext.RefreshTokens
            .Where(t => t.UserId == targetUserId && !t.IsRevoked)
            .ToListAsync(cancellationToken);

        if (activeTokens.Count > 0)
        {
            foreach (var token in activeTokens)
            {
                token.IsRevoked = true;
                token.RevokedAt = now;
                token.UpdatedAt = now;
            }
        }

        // 5. Record audit log
        var newValues = new Dictionary<string, object?>
        {
            ["IsDeleted"] = true,
            ["DeletedAt"] = now,
            ["Status"] = UserStatus.Deleted.ToString().ToUpperInvariant(),
            ["Reason"] = dto.Reason.Trim(),
            ["RevokedTokensCount"] = activeTokens.Count
        };

        _dbContext.AuditLogs.Add(new AuditLog
        {
            ServiceName = "user-service",
            Action = AuditAction.SoftDelete,
            EntityName = "User",
            EntityId = targetUserId.ToString(),
            UserId = adminId?.ToString(),
            OldValues = JsonSerializer.Serialize(oldValues),
            NewValues = JsonSerializer.Serialize(newValues),
            ChangedColumns = JsonSerializer.Serialize(new[] { "IsDeleted", "DeletedAt", "Status" }),
            CreatedAt = now
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[ADMIN USER DELETE] Successfully soft-deleted user '{UserId}'. Revoked {TokenCount} active token(s). Reason: '{Reason}'",
            targetUserId, activeTokens.Count, dto.Reason.Trim());
    }
}
