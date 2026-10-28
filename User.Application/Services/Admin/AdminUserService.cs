namespace User.Application.Services.Admin;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
}
