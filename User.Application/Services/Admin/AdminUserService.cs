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

        // 1. Kiểm tra Email
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

        // 2. Kiểm tra Mật khẩu (8 - 72 ký tự theo chuẩn an toàn của BCrypt)
        if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 8)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_PASSWORD", "Password must be at least 8 characters.");
        }

        if (dto.Password.Length > 72)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_PASSWORD", "Password cannot exceed 72 characters.");
        }

        // 3. Kiểm tra Họ và Tên
        if (string.IsNullOrWhiteSpace(dto.FirstName))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_FIRST_NAME", "First name is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.LastName))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_LAST_NAME", "Last name is required.");
        }

        // 4. Kiểm tra Username (nếu có cung cấp)
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

        // 5. Phân giải danh sách vai trò (RoleCodes)
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

        // Nếu Admin không chọn vai trò cụ thể, mặc định cấp SupportStaff cho nhân sự nội bộ
        if (requestedRoles.Count == 0)
        {
            requestedRoles.Add(RoleType.SupportStaff);
        }

        // 6. Băm mật khẩu bằng BCrypt có Salt
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

        // 7. Tạo bản ghi User mới
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

        // 8. Gán các vai trò vào bảng UserRoles
        var assignedRoleNames = new List<string>();
        foreach (var roleType in requestedRoles)
        {
            var roleEntity = await _dbContext.Roles
                .FirstOrDefaultAsync(r => r.RoleCode == roleType, cancellationToken);

            if (roleEntity == null)
            {
                // Fallback nếu role chưa có trong DB (ví dụ môi trường test in-memory)
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

        // 9. Khởi tạo điểm uy tín ban đầu (ReputationScore = 50 - Standard)
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

        // 10. Ghi nhật ký kiểm toán (AuditLog) theo đúng enum RoleAssign của Lead
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

        // 11. Lưu vào CSDL
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

        // 1. Tìm kiếm theo từ khóa (keyword)
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

        // 2. Lọc theo trạng thái (status)
        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
        {
            var normalizedStatus = status.Trim().ToLowerInvariant().Replace("_", "");
            if (Enum.TryParse<UserStatus>(normalizedStatus, true, out var parsedStatus))
            {
                query = query.Where(u => u.Status == parsedStatus);
            }
            else if (string.Equals(normalizedStatus, "pending", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(u => u.Status == UserStatus.PendingVerification);
            }
        }

        // 3. Lọc theo vai trò (role)
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

        // 4. Đếm tổng số bản ghi (Count)
        var total = await query.CountAsync(cancellationToken);

        // 5. Phân trang (Skip & Take)
        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((safePage - 1) * safeLimit)
            .Take(safeLimit)
            .ToListAsync(cancellationToken);

        // 6. Chuyển đổi sang DTO
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
}
