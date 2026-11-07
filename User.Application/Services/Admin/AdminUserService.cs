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
    private readonly IRedisOtpService? _redisOtpService;
    private readonly IEmailNotificationClient? _emailNotificationClient;

    public AdminUserService(
        IUserDbContext dbContext,
        ILogger<AdminUserService> logger,
        IRedisOtpService? redisOtpService = null,
        IEmailNotificationClient? emailNotificationClient = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _redisOtpService = redisOtpService;
        _emailNotificationClient = emailNotificationClient;
    }

    // =========================================================================================
    // SECTION 1: CẤP PHÁT VÀ QUẢN LÝ THÔNG TIN NGƯỜI DÙNG (USER PROVISIONING & PROFILE UPDATING)
    // =========================================================================================

    /// <summary>
    /// [Admin] Tạo mới tài khoản người dùng bởi Quản trị viên.
    /// Kiểm tra email duy nhất, băm mật khẩu bằng BCrypt có salt, kiểm tra SoD và ép buộc quy tắc 1-User 1-Role.
    /// Tự động ghi AuditLog tập trung sau khi hoàn tất.
    /// </summary>
    /// <param name="dto">Dữ liệu tạo tài khoản: Email, Mật khẩu, Họ tên, Username, Số điện thoại, Vai trò khởi tạo.</param>
    /// <param name="adminId">ID quản trị viên thực hiện.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu.</param>
    /// <returns>Thông tin người dùng mới được tạo.</returns>
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
                        _logger.LogWarning("[ADMIN USER CREATE] Attempt to create seller account directly by Admin '{AdminId}' rejected.", adminId);
                        throw new BusinessRuleValidationException(
                            "CANNOT_CREATE_SELLER",
                            "Seller accounts cannot be created directly by administrator. Sellers must register through the seller onboarding process.");
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

        // Segregation of Duties (SoD): Cannot combine Admin and SupportStaff
        if (requestedRoles.Contains(RoleType.Admin) && requestedRoles.Contains(RoleType.SupportStaff))
        {
            throw new BusinessRuleValidationException("SOD_VIOLATION", "A user cannot be assigned both Admin and SupportStaff roles simultaneously.");
        }

        if (requestedRoles.Count > 1)
        {
            _logger.LogWarning("[ADMIN USER CREATE] Attempt to assign multiple roles [{Roles}] to new user by Admin '{AdminId}' rejected.",
                string.Join(", ", requestedRoles), adminId);
            throw new BusinessRuleValidationException(
                "MULTIPLE_ROLES_NOT_ALLOWED",
                "A user can only be assigned exactly one primary role.");
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

        // 8. Assign role in Users table (1 User 1 Role principle)
        var primaryRoleType = requestedRoles.First();
        var roleEntity = await _dbContext.Roles
            .FirstOrDefaultAsync(r => r.RoleCode == primaryRoleType, cancellationToken);

        if (roleEntity == null)
        {
            roleEntity = new Role
            {
                Id = Guid.NewGuid(),
                RoleCode = primaryRoleType,
                RoleName = primaryRoleType.ToString(),
                Description = $"System {primaryRoleType} role",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _dbContext.Roles.Add(roleEntity);
        }

        newUser.RoleId = roleEntity.Id;
        newUser.Role = roleEntity;
        var assignedRoleNames = new List<string> { roleEntity.RoleName.ToLowerInvariant() };

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
        _dbContext.AddAuditLog(
            action: AuditAction.RoleAssign,
            entityName: nameof(Domain.Entities.User),
            entityId: newUser.Id.ToString(),
            newValues: new
            {
                Email = newUser.Email,
                FirstName = newUser.FirstName,
                LastName = newUser.LastName,
                Roles = assignedRoleNames,
                Status = newUser.Status.ToString(),
                CreatedBy = adminId
            },
            userId: adminId?.ToString() ?? "SYSTEM_ADMIN",
            userName: adminId?.ToString() ?? "SystemAdmin");

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

    // =========================================================================================
    // SECTION 2: TRA CỨU DANH BẠ VÀ CHI TIẾT HỒ SƠ NGƯỜI DÙNG (USER DIRECTORY & QUERIES)
    // =========================================================================================

    /// <summary>
    /// [Admin / Staff] Tra cứu danh sách người dùng trong hệ thống có phân trang và bộ lọc.
    /// Hỗ trợ tìm kiếm theo từ khóa (Email, Username, Họ tên, Phone) và lọc theo Role/Status.
    /// </summary>
    /// <param name="page">Số trang hiện tại (bắt đầu từ 1).</param>
    /// <param name="limit">Số bản ghi mỗi trang (tối đa 100).</param>
    /// <param name="keyword">Từ khóa tìm kiếm (Email, Username, Phone, FullName).</param>
    /// <param name="role">Lọc theo vai trò (Buyer, Seller, Staff, Admin).</param>
    /// <param name="status">Lọc theo trạng thái tài khoản (Active, Banned, PendingVerification).</param>
    /// <param name="sortBy">Trường cần sắp xếp (CreatedAt, Email, FullName).</param>
    /// <param name="sortOrder">Thứ tự sắp xếp (asc, desc).</param>
    /// <param name="cancellationToken">Token hủy yêu cầu.</param>
    /// <returns>Danh sách người dùng phân trang kèm siêu dữ liệu.</returns>
    public async Task<PagedData<AdminUserListItemDto>> GetUsersAsync(
        int page = 1,
        int limit = 20,
        string? keyword = null,
        string? role = null,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        var (safePage, safeLimit) = PaginationConstants.Normalize(page, limit);

        var query = _dbContext.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(u => u.Role)
                .ThenInclude(r => r!.ParentRole)
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

        else
        {
            query = query.Where(u => !u.IsDeleted);
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
                query = query.Where(u => u.Role != null && u.Role.RoleCode == targetRoleType.Value);
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
            var roles = u.GetInheritedRoleClaims();

            if (roles.Count == 0)
            {
                roles.Add("buyer");
            }

            var displayName = $"{u.FirstName} {u.LastName}".Trim();
            var repScore = u.ReputationScore?.CurrentScore ?? 50;
            var trustLevel = (u.ReputationScore?.TrustLevel ?? TrustLevel.Standard).ToString();
            var sellerRepScore = u.ReputationScore?.SellerScore ?? 50;
            var sellerTrustLevel = (u.ReputationScore?.SellerTrustLevel ?? TrustLevel.Standard).ToString();
            var isSeller = roles.Contains("seller") || (u.SellerProfile != null && (u.SellerProfile.Status == SellerProfileStatus.Approved || u.SellerProfile.Status == SellerProfileStatus.Suspended));

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
                SellerReputationScore: sellerRepScore,
                SellerTrustLevel: sellerTrustLevel,
                IsSeller: isSeller,
                Roles: roles,
                Status: u.Status.ToString(),
                CreatedAt: u.CreatedAt
            );
        }).ToList();

        return PagedData<AdminUserListItemDto>.From(items, safePage, safeLimit, total);
    }

    /// <summary>
    /// [Admin / Staff] Xem thông tin chi tiết hồ sơ tài khoản người dùng theo ID.
    /// Tự động loại bỏ các vai trò đã xóa mềm (IsDeleted = true) để trả về đúng vai trò đơn nhất đang hoạt động.
    /// </summary>
    /// <param name="userId">ID người dùng cần tra cứu.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu.</param>
    /// <returns>Chi tiết toàn bộ hồ sơ người dùng, vai trò, gian hàng và điểm uy tín.</returns>
    public async Task<AdminUserDetailDto> GetUserDetailAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[AdminUserService.GetUserDetailAsync] Fetching user profile detail for UserId: {UserId}", userId);

        var user = await _dbContext.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(u => u.Role!)
                .ThenInclude(r => r.ParentRole)
            .Include(u => u.Role!)
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
        var roles = user.GetInheritedRoleClaims();

        // Permissions
        var permissions = user.Role?.RolePermissions?
            .Where(rp => rp.Permission != null)
            .Select(rp => rp.Permission!.PermissionCode.ToLowerInvariant())
            .Distinct()
            .OrderBy(p => p)
            .ToList() ?? new List<string>();

        // Reputation
        var repScore = user.ReputationScore?.CurrentScore ?? 50;
        var trustLevel = (user.ReputationScore?.TrustLevel ?? TrustLevel.Standard).ToString();
        var sellerRepScore = user.ReputationScore?.SellerScore ?? 50;
        var sellerTrustLevel = (user.ReputationScore?.SellerTrustLevel ?? TrustLevel.Standard).ToString();

        // Seller Profile
        AdminSellerProfileDetailDto? sellerProfileDto = null;
        var isSeller = roles.Contains("seller") || (user.SellerProfile != null && (user.SellerProfile.Status == SellerProfileStatus.Approved || user.SellerProfile.Status == SellerProfileStatus.Suspended));

        if (user.SellerProfile != null)
        {
            var sp = user.SellerProfile;
            sellerProfileDto = new AdminSellerProfileDetailDto(
                ShopName: sp.ShopName,
                Status: sp.Status.ToString(),
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
                TargetRole: l.TargetRole.ToString(),
                ChangeAmount: l.ChangeAmount,
                ScoreAfter: l.ScoreAfter,
                Reason: l.Reason.ToString(),
                ReferenceType: l.ReferenceType?.ToString(),
                ReferenceId: l.ReferenceId,
                Note: l.Note,
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
            Status: user.Status.ToString(),
            IsSeller: isSeller,
            ReputationScore: repScore,
            TrustLevel: trustLevel,
            SellerReputationScore: sellerRepScore,
            SellerTrustLevel: sellerTrustLevel,
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

    /// <summary>
    /// [Admin] Lấy danh mục tất cả vai trò khả dụng trong hệ thống.
    /// </summary>
    /// <param name="cancellationToken">Token hủy yêu cầu.</param>
    /// <returns>Danh sách vai trò và quyền hạn.</returns>
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

    // =========================================================================================
    // SECTION 3: QUẢN TRỊ PHÂN QUYỀN VÀ RÀO CHẮN SOD (ROLE ASSIGNMENT & SECURITY GOVERNANCE)
    // =========================================================================================

    /// <summary>
    /// [Admin] Phân quyền và gán vai trò người dùng.
    /// Áp dụng 3 tầng chốt chặn bảo mật bất biến:
    /// 1. Phân tách trách nhiệm (SoD): Cấm gán đồng thời vai trò Admin và SupportStaff.
    /// 2. Nguyên tắc 1-User 1-Primary-Role: Cấm truyền nhiều hơn 1 vai trò (requestedRoles.Count > 1).
    /// 3. Bảo vệ an toàn gian hàng: Chặn tước vai trò Seller nếu Shop đang Active (chưa bị đình chỉ).
    /// </summary>
    /// <param name="userId">ID người dùng cần phân quyền.</param>
    /// <param name="dto">Danh sách mã vai trò (RoleCodes) yêu cầu.</param>
    /// <param name="adminId">ID quản trị viên thực hiện.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu.</param>
    /// <returns>Xác nhận vai trò mới đã được gán thành công.</returns>
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
            .Include(u => u.Role)
            .Include(u => u.SellerProfile)
            .FirstOrDefaultAsync(u => u.Id == targetUserId && !u.IsDeleted, cancellationToken);

        if (targetUser == null)
        {
            throw new DomainException("USER_NOT_FOUND", "User was not found in the system.");
        }

        var allDbRoles = await _dbContext.Roles
            .Where(r => !r.IsDeleted)
            .ToListAsync(cancellationToken);

        // 2. Resolve requested role list
        var requestedRoles = new HashSet<RoleType>();
        if (dto.RoleCodes != null && dto.RoleCodes.Count > 0)
        {
            foreach (var code in dto.RoleCodes)
            {
                if (string.IsNullOrWhiteSpace(code))
                {
                    throw new BusinessRuleValidationException("VAL_INVALID_ROLE", "Role code cannot be empty or whitespace.");
                }

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
                    default:
                        throw new BusinessRuleValidationException("VAL_INVALID_ROLE", $"Role code '{code}' is not recognized.");
                }
            }
        }

        // Handle RoleIds if passed as GUID or integer string
        if (dto.RoleIds != null && dto.RoleIds.Count > 0)
        {
            foreach (var rId in dto.RoleIds)
            {
                if (string.IsNullOrWhiteSpace(rId))
                {
                    throw new BusinessRuleValidationException("VAL_INVALID_ROLE", "Role identifier cannot be empty or whitespace.");
                }

                var trimmedId = rId.Trim();

                if (Guid.TryParse(trimmedId, out var parsedGuid))
                {
                    var matchedRole = allDbRoles.FirstOrDefault(r => r.Id == parsedGuid);
                    if (matchedRole == null)
                    {
                        throw new BusinessRuleValidationException("VAL_INVALID_ROLE", $"Role with ID '{trimmedId}' was not found.");
                    }
                    requestedRoles.Add(matchedRole.RoleCode);
                }
                else if (int.TryParse(trimmedId, out var parsedInt) && Enum.IsDefined(typeof(RoleType), parsedInt))
                {
                    requestedRoles.Add((RoleType)parsedInt);
                }
                else
                {
                    throw new BusinessRuleValidationException("VAL_INVALID_ROLE", $"Role identifier '{trimmedId}' is invalid.");
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
            throw new BusinessRuleValidationException("SOD_VIOLATION", "A user cannot be assigned both Admin and SupportStaff roles simultaneously.");
        }

        // 4. Single Primary Role Guard: Exactly 1 primary role allowed
        if (requestedRoles.Count > 1)
        {
            _logger.LogWarning("[ADMIN ASSIGN ROLES] Attempt to assign multiple roles [{Roles}] to User '{UserId}' by Admin '{AdminId}' rejected.",
                string.Join(", ", requestedRoles), targetUserId, adminId);
            throw new BusinessRuleValidationException(
                "MULTIPLE_ROLES_NOT_ALLOWED",
                "A user can only be assigned exactly one primary role.");
        }

        // 4. Demotion Guard: Cannot demote the last active Administrator
        var currentRole = targetUser.Role?.RoleCode;

        if (currentRole == RoleType.Admin && !requestedRoles.Contains(RoleType.Admin))
        {
            var activeAdminCount = await _dbContext.Users
                .Include(u => u.Role)
                .Where(u => !u.IsDeleted && u.Status == UserStatus.Active && u.Role != null && u.Role.RoleCode == RoleType.Admin)
                .CountAsync(cancellationToken);

            if (activeAdminCount <= 1)
            {
                throw new BusinessRuleValidationException("CANNOT_DEMOTE_LAST_ADMIN", "Cannot remove Admin role from the last active Administrator in the system.");
            }
        }

        // 5. Seller Role Guards
        if (currentRole != RoleType.Seller && requestedRoles.Contains(RoleType.Seller))
        {
            _logger.LogWarning("[ADMIN ASSIGN ROLES] Attempt to assign seller role directly to User '{UserId}' by Admin '{AdminId}' rejected.", targetUserId, adminId);
            throw new BusinessRuleValidationException(
                "CANNOT_ASSIGN_SELLER_ROLE_DIRECTLY",
                "Seller role cannot be assigned directly. It must be granted through the seller registration approval workflow.");
        }

        if (currentRole == RoleType.Seller && !requestedRoles.Contains(RoleType.Seller))
        {
            var isShopSuspended = targetUser.SellerProfile != null && targetUser.SellerProfile.Status == SellerProfileStatus.Suspended;
            if (!isShopSuspended)
            {
                _logger.LogWarning("[ADMIN ASSIGN ROLES] Attempt to revoke seller role directly from User '{UserId}' by Admin '{AdminId}' rejected.", targetUserId, adminId);
                throw new BusinessRuleValidationException(
                    "CANNOT_REVOKE_SELLER_ROLE_DIRECTLY",
                    "Seller role cannot be removed directly from this endpoint. Please use the dedicated seller suspension endpoint: PUT /api/v1/admin/sellers/{sellerId}/suspend.");
            }
        }

        // 6. Update RoleId (1 User 1 Role principle)
        var oldRoleNames = currentRole.HasValue ? new List<string> { currentRole.Value.ToString() } : new List<string>();
        var newRoleType = requestedRoles.First();
        var targetDbRole = allDbRoles.FirstOrDefault(r => r.RoleCode == newRoleType);
        if (targetDbRole == null)
        {
            throw new DomainException("ROLE_NOT_FOUND", $"Role '{newRoleType}' was not found in the system.");
        }

        targetUser.RoleId = targetDbRole.Id;
        targetUser.Role = targetDbRole;

        var newRoleNames = requestedRoles.Select(r => r.ToString().ToLowerInvariant()).ToList();

        // 7. Revoke active refresh tokens to force session refresh with updated roles
        await RevokeActiveRefreshTokensAsync(targetUserId, DateTimeOffset.UtcNow, cancellationToken);

        // 8. Ghi AuditLog
        _dbContext.AddAuditLog(
            action: AuditAction.RoleAssign,
            entityName: "Users",
            entityId: targetUserId.ToString(),
            oldValues: oldRoleNames,
            newValues: newRoleNames,
            userId: adminId?.ToString(),
            changedColumns: new[] { "RoleId" });

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[ADMIN ASSIGN ROLES] Successfully updated roles for User '{UserId}'. New roles: {Roles}",
            targetUserId, string.Join(", ", newRoleNames));

        return new AdminUserRolesResponseDto(
            UserId: targetUserId.ToString(),
            Roles: newRoleNames,
            UpdatedAt: DateTimeOffset.UtcNow
        );
    }

    /// <summary>
    /// [Admin] Cập nhật thông tin tài khoản người dùng (Họ tên, Số điện thoại, Trạng thái).
    /// Tự động ghi nhật ký AuditLog chi tiết các cột đã thay đổi (OldValues vs NewValues).
    /// </summary>
    /// <param name="userId">ID người dùng cần cập nhật.</param>
    /// <param name="dto">Dữ liệu cập nhật hồ sơ.</param>
    /// <param name="adminId">ID quản trị viên thực hiện.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu.</param>
    /// <returns>Hồ sơ người dùng sau khi cập nhật.</returns>
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
            ["Status"] = user.Status.ToString()
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
            ["Status"] = user.Status.ToString(),
            ["Reason"] = dto.Reason.Trim()
        };

        // 7. Ghi AuditLog
        _dbContext.AddAuditLog(
            action: AuditAction.UserUpdate,
            entityName: "User",
            entityId: targetUserId.ToString(),
            oldValues: oldValues,
            newValues: newValues,
            userId: adminId?.ToString(),
            changedColumns: changedColumns);

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[ADMIN USER UPDATE] Successfully updated user '{UserId}'. Changed columns: {ChangedCols}",
            targetUserId, string.Join(", ", changedColumns));

        return new AdminUserUpdatedResponseDto(
            Id: user.Id.ToString(),
            UserId: user.Id.ToString(),
            FirstName: user.FirstName,
            LastName: user.LastName,
            PhoneNumber: user.PhoneNumber,
            Status: user.Status.ToString(),
            UpdatedBy: adminId?.ToString() ?? "system",
            UpdatedAt: user.UpdatedAt,
            Message: "User updated successfully."
        );
    }

    // =========================================================================================
    // SECTION 4: KỶ LUẬT, XỬ PHẠT VÀ VÒNG ĐỜI TÀI KHOẢN (SANCTIONS & ACCOUNT LIFECYCLE)
    // =========================================================================================

    /// <summary>
    /// [Admin] Xóa mềm tài khoản người dùng (Soft Delete: IsDeleted = true).
    /// Đồng thời vô hiệu hóa tất cả UserRoles và thu hồi toàn bộ Refresh Tokens.
    /// </summary>
    /// <param name="userId">ID người dùng cần xóa.</param>
    /// <param name="adminId">ID quản trị viên thực hiện.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu.</param>
    public async Task DeleteUserAsync(
        Guid targetUserId,
        DeleteAdminUserDto? dto,
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
            .Include(u => u.Role)
            .Include(u => u.SellerProfile)
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
        var isTargetAdmin = user.Role != null && user.Role.RoleCode == RoleType.Admin;

        if (isTargetAdmin)
        {
            var activeAdminCount = await _dbContext.Users
                .Include(u => u.Role)
                .Where(u => !u.IsDeleted && u.Status == UserStatus.Active && u.Role != null && u.Role.RoleCode == RoleType.Admin)
                .CountAsync(cancellationToken);

            if (activeAdminCount <= 1)
            {
                throw new BusinessRuleValidationException("CANNOT_DELETE_LAST_ADMIN", "Cannot delete the last active Administrator in the system.");
            }
        }

        var now = DateTimeOffset.UtcNow;
        var oldValues = new Dictionary<string, object?>
        {
            ["IsDeleted"] = user.IsDeleted,
            ["Status"] = user.Status.ToString()
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
        var revokedTokensCount = await RevokeActiveRefreshTokensAsync(targetUserId, now, cancellationToken);

        // 5. Record audit log
        var newValues = new Dictionary<string, object?>
        {
            ["IsDeleted"] = true,
            ["DeletedAt"] = now,
            ["Status"] = UserStatus.Deleted.ToString().ToUpperInvariant(),
            ["Reason"] = dto.Reason.Trim(),
            ["RevokedTokensCount"] = revokedTokensCount
        };

        _dbContext.AddAuditLog(
            action: AuditAction.SoftDelete,
            entityName: "User",
            entityId: targetUserId.ToString(),
            oldValues: oldValues,
            newValues: newValues,
            userId: adminId?.ToString(),
            changedColumns: new[] { "IsDeleted", "DeletedAt", "Status" });

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[ADMIN USER DELETE] Successfully soft-deleted user '{UserId}'. Revoked {TokenCount} active token(s). Reason: '{Reason}'",
            targetUserId, revokedTokensCount, dto.Reason.Trim());
    }

    /// <summary>
    /// [Admin] Cấm (khóa) tài khoản người dùng do vi phạm chính sách nghiêm trọng.
    /// Tự động tạo bản ghi biên bản UserPenalty, hủy toàn bộ phiên làm việc trên Redis và DB, ghi AuditLog.
    /// </summary>
    /// <param name="userId">ID người dùng bị khóa.</param>
    /// <param name="dto">Lý do khóa và thời hạn khóa tài khoản.</param>
    /// <param name="adminId">ID quản trị viên thực hiện.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu.</param>
    /// <returns>Xác nhận khóa tài khoản thành công.</returns>
    public async Task<AdminUserBannedResponseDto> BanUserAsync(
        Guid targetUserId,
        BanAdminUserDto dto,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        if (targetUserId == Guid.Empty)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Target user ID is required.");
        }

        if (dto == null || string.IsNullOrWhiteSpace(dto.Reason))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Reason for banning user is required.");
        }

        var trimmedReason = dto.Reason.Trim();
        if (trimmedReason.Length > 1000)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Ban reason cannot exceed 1000 characters.");
        }

        if (adminId.HasValue && adminId.Value == targetUserId)
        {
            throw new BusinessRuleValidationException("CANNOT_BAN_SELF", "Administrators cannot ban their own account.");
        }

        // 1. Find user in DB (including soft-deleted for correct error status)
        var user = await _dbContext.Users
            .IgnoreQueryFilters()
            .Include(u => u.Role)
            .Include(u => u.SellerProfile)
            .FirstOrDefaultAsync(u => u.Id == targetUserId, cancellationToken);

        if (user == null)
        {
            _logger.LogWarning("[ADMIN USER BAN] User '{UserId}' not found.", targetUserId);
            throw new DomainException("USER_NOT_FOUND", "User was not found in the system.", 404);
        }

        if (user.IsDeleted)
        {
            _logger.LogWarning("[ADMIN USER BAN] User '{UserId}' has already been deleted.", targetUserId);
            throw new DomainException("USER_ALREADY_DELETED", "User has already been deleted.", 400);
        }

        if (user.Status == UserStatus.Banned)
        {
            _logger.LogWarning("[ADMIN USER BAN] User '{UserId}' is already banned.", targetUserId);
            throw new DomainException("USER_ALREADY_BANNED", "User account is already banned.", 400);
        }

        // 2. Guard against banning the last active Administrator
        var isTargetAdmin = user.Role != null && user.Role.RoleCode == RoleType.Admin;

        if (isTargetAdmin)
        {
            var activeAdminCount = await _dbContext.Users
                .Include(u => u.Role)
                .Where(u => !u.IsDeleted && u.Status == UserStatus.Active && u.Role != null && u.Role.RoleCode == RoleType.Admin)
                .CountAsync(cancellationToken);

            if (activeAdminCount <= 1)
            {
                throw new BusinessRuleValidationException("CANNOT_BAN_LAST_ADMIN", "Cannot ban the last active Administrator in the system.");
            }
        }

        var now = DateTimeOffset.UtcNow;
        var oldValues = new Dictionary<string, object?>
        {
            ["Status"] = user.Status.ToString(),
            ["BannedAt"] = user.BannedAt,
            ["BannedBy"] = user.BannedBy?.ToString(),
            ["BanReason"] = user.BanReason
        };

        // 3. Update User Entity via encapsulated domain method
        user.ApplyAccountBan(trimmedReason, adminId);

        // 3.1. Suspend associated seller profile if present
        if (user.SellerProfile != null && !user.SellerProfile.IsDeleted && user.SellerProfile.Status != SellerProfileStatus.Suspended)
        {
            user.SellerProfile.Suspend(trimmedReason, adminId);
        }

        // 3.2. Record account-level sanction in UserPenalties ledger
        var accountPenalty = new UserPenalty
        {
            Id = Guid.NewGuid(),
            UserId = targetUserId,
            TargetRole = TargetRole.Account,
            ViolationType = PenaltyViolationType.Fraud,
            Reason = trimmedReason,
            PenaltyApplied = PenaltyType.BanAccount,
            PointsDeducted = 0,
            Status = PenaltyStatus.Active,
            IssuedAt = now,
            ExpiresAt = null,
            CreatedBy = adminId,
            PolicyClauseCode = dto?.PolicyClauseCode ?? "ACCOUNT_TERMS_VIOLATION",
            EvidenceUrls = dto?.EvidenceUrls ?? new List<string>(),
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false
        };
        _dbContext.UserPenalties.Add(accountPenalty);

        // 4. Revoke active refresh tokens in PostgreSQL
        var revokedTokensCount = await RevokeActiveRefreshTokensAsync(targetUserId, now, cancellationToken);

        // 5. Audit Log
        var newValues = new Dictionary<string, object?>
        {
            ["Status"] = UserStatus.Banned.ToString(),
            ["BannedAt"] = now,
            ["BannedBy"] = adminId?.ToString(),
            ["BanReason"] = trimmedReason,
            ["RevokedTokensCount"] = revokedTokensCount
        };

        _dbContext.AddAuditLog(
            action: AuditAction.UserBan,
            entityName: "User",
            entityId: targetUserId.ToString(),
            oldValues: oldValues,
            newValues: newValues,
            userId: adminId?.ToString(),
            changedColumns: new[] { "Status", "BannedAt", "BannedBy", "BanReason" });

        await _dbContext.SaveChangesAsync(cancellationToken);

        // 6. Redis Invalidation
        if (_redisOtpService != null)
        {
            try
            {
                await _redisOtpService.RevokeUserTokensAsync(targetUserId, TimeSpan.FromDays(30));
                await _redisOtpService.LockAccountAsync(user.Email, TimeSpan.FromDays(30));
                await _redisOtpService.LockAccountAsync(targetUserId.ToString(), TimeSpan.FromDays(30));
                await _redisOtpService.InvalidateExistingOtpsAsync(user.Email, "registration");
                await _redisOtpService.InvalidateExistingOtpsAsync(user.Email, "password_reset");
                await _redisOtpService.InvalidateExistingOtpsAsync(user.Email, "seller_onboarding");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ADMIN USER BAN] Error invalidating Redis session/tokens for User '{UserId}'.", targetUserId);
            }
        }

        _logger.LogInformation("[ADMIN USER BAN] Successfully banned user '{UserId}'. Revoked {TokenCount} active token(s). Reason: '{Reason}'",
            targetUserId, revokedTokensCount, trimmedReason);

        // Dispatch ban notification email via Notification Service
        if (_emailNotificationClient != null && !string.IsNullOrWhiteSpace(user.Email))
        {
            var recipientEmail = user.Email.Trim();
            var fullName = $"{user.FirstName} {user.LastName}".Trim();
            if (string.IsNullOrWhiteSpace(fullName))
            {
                fullName = recipientEmail;
            }

            try
            {
                await _emailNotificationClient.SendUserBannedEmailAsync(
                    recipientEmail,
                    fullName,
                    trimmedReason,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ADMIN USER BAN] Failed to dispatch ban notification email to '{Email}'.", recipientEmail);
            }
        }

        return new AdminUserBannedResponseDto(
            UserId: user.Id.ToString(),
            Status: user.Status.ToString(),
            BanReason: trimmedReason,
            BannedAt: now,
            BannedBy: adminId?.ToString() ?? "system",
            RevokedTokensCount: revokedTokensCount,
            Message: "User account banned successfully."
        );
    }

    /// <summary>
    /// [Admin] Gỡ cấm (mở khóa) cho tài khoản người dùng sau khi hết hạn hoặc khắc phục vi phạm.
    /// Thu hồi bản ghi UserPenalty tương ứng, khôi phục trạng thái UserStatus = Active, ghi AuditLog.
    /// </summary>
    /// <param name="userId">ID người dùng được mở khóa.</param>
    /// <param name="dto">Lý do mở khóa.</param>
    /// <param name="adminId">ID quản trị viên thực hiện.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu.</param>
    /// <returns>Xác nhận mở khóa tài khoản thành công.</returns>
    public async Task<AdminUserUnbannedResponseDto> UnbanUserAsync(
        Guid targetUserId,
        UnbanAdminUserDto? dto = null,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        if (targetUserId == Guid.Empty)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Target user ID is required.");
        }

        // 1. Find user in DB
        var user = await _dbContext.Users
            .IgnoreQueryFilters()
            .Include(u => u.SellerProfile)
            .FirstOrDefaultAsync(u => u.Id == targetUserId, cancellationToken);

        if (user == null)
        {
            _logger.LogWarning("[ADMIN USER UNBAN] User '{UserId}' not found.", targetUserId);
            throw new DomainException("USER_NOT_FOUND", "User was not found in the system.", 404);
        }

        if (user.IsDeleted)
        {
            _logger.LogWarning("[ADMIN USER UNBAN] User '{UserId}' has already been deleted.", targetUserId);
            throw new DomainException("USER_ALREADY_DELETED", "User has already been deleted.", 400);
        }

        if (user.Status != UserStatus.Banned)
        {
            _logger.LogWarning("[ADMIN USER UNBAN] User '{UserId}' is not currently banned. Current status: {Status}", targetUserId, user.Status);
            throw new DomainException("USER_NOT_BANNED", "User account is not currently banned.", 400);
        }

        var now = DateTimeOffset.UtcNow;
        var oldValues = new Dictionary<string, object?>
        {
            ["Status"] = user.Status.ToString(),
            ["BannedAt"] = user.BannedAt,
            ["BannedBy"] = user.BannedBy?.ToString(),
            ["BanReason"] = user.BanReason
        };

        // 2. Restore Status: Active if email verified, otherwise PendingVerification
        var restoredStatus = user.EmailVerifiedAt.HasValue ? UserStatus.Active : UserStatus.PendingVerification;
        user.Status = restoredStatus;
        user.BannedAt = null;
        user.BannedBy = null;
        user.BanReason = null;
        user.UpdatedAt = now;
        if (adminId.HasValue)
        {
            user.UpdatedBy = adminId.Value;
        }

        var reason = !string.IsNullOrWhiteSpace(dto?.Reason) ? dto.Reason.Trim() : "Account unbanned by administrator.";

        // Reactivate seller profile if it was suspended due to ban AND no independent seller penalties remain active
        if (user.SellerProfile != null && !user.SellerProfile.IsDeleted && user.SellerProfile.Status == SellerProfileStatus.Suspended)
        {
            var hasActiveSellerPenalties = await _dbContext.UserPenalties
                .AnyAsync(p => p.UserId == targetUserId &&
                               p.TargetRole == TargetRole.Seller &&
                               p.Status == PenaltyStatus.Active &&
                               (p.ExpiresAt == null || p.ExpiresAt > now), cancellationToken);

            if (!hasActiveSellerPenalties)
            {
                user.SellerProfile.Reactivate();
            }
        }

        // Revoke active account penalties
        var activeAccountPenalties = await _dbContext.UserPenalties
            .Where(p => p.UserId == targetUserId && p.TargetRole == TargetRole.Account && p.Status == PenaltyStatus.Active)
            .ToListAsync(cancellationToken);

        foreach (var penalty in activeAccountPenalties)
        {
            penalty.Revoke(adminId ?? Guid.Empty, reason);
        }

        var newValues = new Dictionary<string, object?>
        {
            ["Status"] = restoredStatus.ToString().ToUpperInvariant(),
            ["BannedAt"] = null,
            ["BannedBy"] = null,
            ["BanReason"] = null,
            ["Reason"] = reason
        };

        // 3. Audit Log
        _dbContext.AddAuditLog(
            action: AuditAction.UserUnban,
            entityName: "User",
            entityId: targetUserId.ToString(),
            oldValues: oldValues,
            newValues: newValues,
            userId: adminId?.ToString(),
            changedColumns: new[] { "Status", "BannedAt", "BannedBy", "BanReason" });

        await _dbContext.SaveChangesAsync(cancellationToken);

        // 4. Redis Cleanup
        if (_redisOtpService != null)
        {
            try
            {
                await _redisOtpService.UnbanUserAsync(targetUserId);
                await _redisOtpService.ResetLoginAttemptsAsync(user.Email);
                await _redisOtpService.ResetLoginAttemptsAsync(targetUserId.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ADMIN USER UNBAN] Error clearing Redis ban/lockout for User '{UserId}'.", targetUserId);
            }
        }

        _logger.LogInformation("[ADMIN USER UNBAN] Successfully unbanned user '{UserId}'. Restored status to '{Status}'.",
            targetUserId, restoredStatus);

        // Dispatch unban notification email via Notification Service
        if (_emailNotificationClient != null && !string.IsNullOrWhiteSpace(user.Email))
        {
            var recipientEmail = user.Email.Trim();
            var fullName = $"{user.FirstName} {user.LastName}".Trim();
            if (string.IsNullOrWhiteSpace(fullName))
            {
                fullName = recipientEmail;
            }

            try
            {
                await _emailNotificationClient.SendUserUnbannedEmailAsync(
                    recipientEmail,
                    fullName,
                    reason,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ADMIN USER UNBAN] Failed to dispatch unban notification email to '{Email}'.", recipientEmail);
            }
        }

        return new AdminUserUnbannedResponseDto(
            UserId: user.Id.ToString(),
            Status: restoredStatus.ToString(),
            UnbannedAt: now,
            UnbannedBy: adminId?.ToString() ?? "system",
            Message: "User account unbanned successfully."
        );
    }

    // =========================================================================================
    // SECTION 5: HÀM PHỤ TRỢ BẢO MẬT VÀ PHIÊN LÀM VIỆC (PRIVATE SECURITY HELPERS)
    // =========================================================================================

    private async Task<int> RevokeActiveRefreshTokensAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var activeTokens = await _dbContext.RefreshTokens
            .Where(t => t.UserId == userId && !t.IsRevoked)
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

        return activeTokens.Count;
    }
}
