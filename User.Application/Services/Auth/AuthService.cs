namespace User.Application.Services.Auth;

using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using User.Application.Common.Extensions;
using User.Application.Interfaces;
using User.Application.Models;
using User.Domain.Entities;
using User.Domain.Enums;
using User.Domain.Exceptions;

public class AuthService : IAuthService
{
    private readonly IUserDbContext _dbContext;
    private readonly IRedisOtpService _otpService;
    private readonly IJwtTokenGenerator? _jwtTokenGenerator;
    private readonly IEmailNotificationClient? _emailNotificationClient;
    private readonly ILogger<AuthService> _logger;



    public AuthService(
        IUserDbContext dbContext,
        IRedisOtpService otpService,
        ILogger<AuthService> logger,
        IJwtTokenGenerator? jwtTokenGenerator = null,
        IEmailNotificationClient? emailNotificationClient = null)
    {
        _dbContext = dbContext;
        _otpService = otpService;
        _logger = logger;
        _jwtTokenGenerator = jwtTokenGenerator;
        _emailNotificationClient = emailNotificationClient;
    }

    // =========================================================================================
    // SECTION 1: ĐĂNG KÝ TÀI KHOẢN VÀ XÁC THỰC OTP EMAIL (REGISTRATION & OTP VERIFICATION)
    // =========================================================================================

    /// <summary>
    /// Đăng ký tài khoản người dùng mới (vai trò mặc định: Buyer).
    /// Băm mật khẩu bằng BCrypt có salt, tạo user trạng thái PendingVerification, sinh OTP lưu Redis và gửi email.
    /// </summary>
    /// <param name="dto">Dữ liệu đăng ký: Email, Mật khẩu, Họ tên, Username, Số điện thoại.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu.</param>
    /// <returns>Thực thể User vừa tạo.</returns>
    public async Task<User> RegisterAsync(RegisterDto dto, CancellationToken cancellationToken = default)
    {
        var email = dto.Email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || !Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_EMAIL", "Email address is invalid.");
        }

        if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 8)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_PASSWORD", "Password must be at least 8 characters.");
        }

        if (string.IsNullOrWhiteSpace(dto.FirstName))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_FIRST_NAME", "First name is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.LastName))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_LAST_NAME", "Last name is required.");
        }

        string? username = null;
        if (!string.IsNullOrWhiteSpace(dto.Username))
        {
            username = dto.Username.Trim();
            if (username.Length < 3 || username.Length > 50)
            {
                throw new BusinessRuleValidationException("VAL_INVALID_USERNAME", "Username must be between 3 and 50 characters.");
            }

            var usernameLower = username.ToLowerInvariant();
            var usernameTaken = await _dbContext.Users.AnyAsync(
                u => u.Username != null && u.Username.ToLower() == usernameLower && u.Email != email,
                cancellationToken);

            if (usernameTaken)
            {
                throw new DomainException("USERNAME_ALREADY_EXISTS", "Username is already registered in the system.");
            }
        }

        var existingUser = await _dbContext.Users
            .Include(u => u.Role)
                .ThenInclude(r => r!.ParentRole)
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        User user;

        if (existingUser != null)
        {
            if (existingUser.Status == UserStatus.Active)
            {
                throw new DomainException("EMAIL_ALREADY_EXISTS", "Email is already registered in the system.");
            }

            if (existingUser.Status == UserStatus.Banned)
            {
                throw new DomainException("ACCOUNT_BANNED", FormatBanMessage(existingUser.BanReason), 403);
            }

            // Re-registration for unverified account (PendingVerification):
            existingUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
            existingUser.FirstName = dto.FirstName.Trim();
            existingUser.LastName = dto.LastName.Trim();
            if (!string.IsNullOrWhiteSpace(dto.PhoneNumber))
            {
                existingUser.PhoneNumber = dto.PhoneNumber.Trim();
            }
            if (!string.IsNullOrWhiteSpace(username))
            {
                existingUser.Username = username;
            }

            await _otpService.InvalidateExistingOtpsAsync(email);
            user = existingUser;
            _logger.LogInformation("Re-registration initiated for unverified account '{Email}'. Old OTP invalidated.", email);
        }
        else
        {
            // Create new User with PendingVerification status
            var userId = Guid.NewGuid();
            user = new User
            {
                Id = userId,
                Username = username,
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                FirstName = dto.FirstName.Trim(),
                LastName = dto.LastName.Trim(),
                PhoneNumber = dto.PhoneNumber?.Trim(),
                Status = UserStatus.PendingVerification,
                Gender = Gender.Unspecified,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                IsDeleted = false
            };

            _dbContext.Users.Add(user);

            // Assign default role: Buyer (clean query by RoleCode Enum, zero hardcoded GUIDs)
            var buyerRole = await _dbContext.Roles.FirstOrDefaultAsync(r => r.RoleCode == RoleType.Buyer, cancellationToken);
            if (buyerRole == null)
            {
                buyerRole = new Role
                {
                    Id = Guid.NewGuid(),
                    RoleCode = RoleType.Buyer,
                    RoleName = "Buyer",
                    Description = "Khách mua hàng và tham gia đấu giá",
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                _dbContext.Roles.Add(buyerRole);
            }
            user.RoleId = buyerRole.Id;
            user.Role = buyerRole;

            // Initialize default reputation score (Standard: 50 points)
            _dbContext.ReputationScores.Add(new ReputationScore
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CurrentScore = 50,
                TrustLevel = TrustLevel.Standard
            });

            _logger.LogInformation("New user registration initiated for '{Email}' with PendingVerification status and Buyer role.", email);
        }

        // Generate random 6-digit OTP code
        var otpCode = Random.Shared.Next(100000, 999999).ToString();
        _logger.LogInformation("[OTP DISPATCH] Email: '{Email}' | CODE: >>> {OtpCode} <<< | Valid: 5 mins", email, otpCode);

        // Store in Redis: Valid for 5 minutes, stored up to 15 minutes
        await _otpService.SaveOtpAsync(email, otpCode, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15));

        // Send OTP verification email via Notification Service REST API
        if (_emailNotificationClient != null)
        {
            var fullName = $"{user.FirstName} {user.LastName}".Trim();
            var sent = await _emailNotificationClient.SendOtpEmailAsync(user.Email, fullName, otpCode, 5, cancellationToken);
            if (sent)
            {
                _logger.LogInformation("Dispatched OTP email via REST Notification client for recipient '{Email}'.", email);
            }
            else
            {
                _logger.LogWarning("Failed to dispatch OTP email via REST Notification client. OTP fallback code: {Otp}", otpCode);
            }
        }
        else
        {
            _logger.LogWarning("[NOTIFICATION CLIENT OFFLINE] Simulated sending OTP email to '{Email}'. OTP: {Otp}", email, otpCode);
        }

        // Commit changes to database
        await _dbContext.SaveChangesAsync(cancellationToken);

        return user;
    }

    /// <summary>
    /// Đăng ký tài khoản qua API RESTful (envelope chuẩn).
    /// </summary>
    /// <param name="request">Payload đăng ký từ client.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu.</param>
    /// <returns>Phản hồi đăng ký chứa ID người dùng và thông báo gửi OTP.</returns>
    public async Task<RegisterApiResponse> RegisterUserAsync(RegisterApiRequest request, CancellationToken cancellationToken = default)
    {
        var dto = new RegisterDto(
            Email: request.Email,
            Password: request.Password,
            FirstName: request.FirstName?.Trim() ?? string.Empty,
            LastName: request.LastName?.Trim() ?? string.Empty,
            PhoneNumber: request.PhoneNumber?.Trim(),
            Username: string.IsNullOrWhiteSpace(request.Username) ? null : request.Username.Trim()
        );

        var user = await RegisterAsync(dto, cancellationToken);

        return new RegisterApiResponse(
            UserId: user.Id.ToString(),
            Email: user.Email,
            OtpExpiresIn: 300
        );
    }

    /// <summary>
    /// Đăng ký tài khoản phục vụ môi trường kiểm thử (trả về trực tiếp mã OTP trong response để test tự động).
    /// </summary>
    /// <param name="request">Payload đăng ký kiểm thử.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu.</param>
    /// <returns>Phản hồi chứa mã OTP.</returns>
    public async Task<RegisterTestApiResponse> RegisterTestUserAsync(RegisterTestApiRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Request payload cannot be empty.");
        }

        var email = request.Email?.Trim() ?? string.Empty;
        var password = request.Password ?? string.Empty;
        var localPart = email.Split('@')[0].Trim();
        var lastName = string.IsNullOrWhiteSpace(localPart) ? "User" : localPart;
        var dto = new RegisterDto(
            Email: email,
            Password: password,
            FirstName: "Test",
            LastName: lastName
        );

        var user = await RegisterAsync(dto, cancellationToken);

        return new RegisterTestApiResponse(
            Success: true,
            Message: "Test user registered with PendingVerification status and Buyer role. OTP dispatched.",
            UserId: user.Id.ToString(),
            Email: user.Email,
            Status: user.Status.ToString(),
            Role: "Buyer",
            OtpSent: _emailNotificationClient != null,
            OtpExpiresIn: 300
        );
    }

    /// <summary>
    /// Xác thực mã OTP email để kích hoạt tài khoản sang trạng thái Active.
    /// </summary>
    /// <param name="email">Email tài khoản cần kích hoạt.</param>
    /// <param name="otp">Mã OTP gồm 6 chữ số.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu.</param>
    /// <returns>Thực thể User sau kích hoạt.</returns>
    public async Task<User> VerifyOtpAsync(string email, string otp, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_EMAIL", "Email address is required.");
        }

        if (string.IsNullOrWhiteSpace(otp))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_OTP", "OTP code is required.");
        }

        var user = await _dbContext.Users
            .Include(u => u.Role)
                .ThenInclude(r => r!.ParentRole)
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (user == null)
        {
            throw new DomainException("USER_NOT_FOUND", "User account not found.");
        }

        if (user.Status == UserStatus.Banned)
        {
            throw new DomainException("ACCOUNT_BANNED", FormatBanMessage(user.BanReason), 403);
        }

        var otpEntry = await _otpService.GetOtpAsync(normalizedEmail);
        if (otpEntry == null)
        {
            _logger.LogWarning("[AUTH] OTP verification failed for '{Email}': OTP code has expired or does not exist.", normalizedEmail);
            throw new DomainException("OTP_EXPIRED", "OTP code has expired or does not exist.");
        }

        if (otpEntry.Status == OtpStatus.Consumed)
        {
            _logger.LogWarning("[AUTH] OTP verification failed for '{Email}': OTP code has already been used.", normalizedEmail);
            throw new DomainException("OTP_ALREADY_USED", "This OTP code has already been used.");
        }

        if (otpEntry.Status == OtpStatus.Expired || DateTimeOffset.UtcNow > otpEntry.ExpiresAt)
        {
            _logger.LogWarning("[AUTH] OTP verification failed for '{Email}': OTP code has expired (ExpiresAt: {ExpiresAt}).", normalizedEmail, otpEntry.ExpiresAt);
            throw new DomainException("OTP_EXPIRED", "OTP code has expired. Please request a new code.");
        }

        if (!string.Equals(otpEntry.Code, otp.Trim(), StringComparison.Ordinal))
        {
            var attempts = await _otpService.IncrementOtpAttemptsAsync(normalizedEmail, TimeSpan.FromMinutes(5));
            _logger.LogWarning("[AUTH] OTP verification failed for '{Email}': Provided '{Provided}' does not match expected OTP. Attempt {Attempts}/5.", normalizedEmail, otp.Trim(), attempts);

            if (attempts >= 5)
            {
                await _otpService.InvalidateExistingOtpsAsync(normalizedEmail);
                _logger.LogWarning("[AUTH] Maximum OTP verification attempts exceeded for '{Email}'. Invalidated OTP.", normalizedEmail);
                throw new DomainException("OTP_MAX_ATTEMPTS_EXCEEDED", "Too many incorrect OTP attempts. The OTP code has been invalidated. Please request a new code.");
            }

            throw new DomainException("OTP_INVALID", "OTP code is invalid.");
        }

        await _otpService.ResetOtpAttemptsAsync(normalizedEmail);

        if (user.Status == UserStatus.Active)
        {
            return user;
        }

        await _otpService.MarkOtpConsumedAsync(normalizedEmail);

        user.Status = UserStatus.Active;
        user.EmailVerifiedAt = DateTimeOffset.UtcNow;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("[AUTH] User '{Email}' successfully verified OTP. Status changed to Active.", normalizedEmail);

        return user;
    }

    /// <summary>
    /// Xác thực OTP kích hoạt tài khoản qua API RESTful.
    /// </summary>
    /// <param name="request">Payload chứa email và mã OTP.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu.</param>
    /// <returns>Phản hồi kích hoạt thành công.</returns>
    public async Task<VerifyOtpApiResponse> VerifyUserOtpAsync(VerifyOtpApiRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Otp))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Email and OTP code are required.");
        }

        var user = await VerifyOtpAsync(request.Email, request.Otp, cancellationToken);

        var roles = new List<string>();
        ExtractUserRoles(user, roles);
        if (roles.Count == 0) roles.Add("buyer");

        return new VerifyOtpApiResponse(
            IsVerified: true,
            Email: user.Email,
            UserId: user.Id.ToString(),
            Role: roles.FirstOrDefault() ?? "buyer",
            Roles: roles,
            Permissions: Array.Empty<string>()
        );
    }

    private static System.Collections.Generic.IReadOnlyList<string> MapUserRoles(User user)
    {
        var roles = new System.Collections.Generic.List<string>();
        ExtractUserRoles(user, roles);
        return roles;
    }

    /// <summary>
    /// Gửi lại mã OTP xác thực email khi mã cũ hết hạn.
    /// </summary>
    /// <param name="email">Email nhận mã OTP.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu.</param>
    /// <returns>True nếu gửi thành công.</returns>
    public async Task<bool> ResendOtpAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_EMAIL", "Email address is required.");
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);
        if (user == null)
        {
            _logger.LogWarning("[AUTH] Resend OTP rejected: User '{Email}' not found.", normalizedEmail);
            throw new DomainException("USER_NOT_FOUND", "User account not found.");
        }

        if (user.Status == UserStatus.Active)
        {
            _logger.LogWarning("[AUTH] Resend OTP rejected: User '{Email}' is already verified.", normalizedEmail);
            throw new DomainException("USER_ALREADY_VERIFIED", "User account has already been verified.");
        }

        if (user.Status == UserStatus.Banned)
        {
            _logger.LogWarning("[AUTH] Resend OTP rejected: User '{Email}' is banned.", normalizedEmail);
            throw new DomainException("ACCOUNT_BANNED", FormatBanMessage(user.BanReason), 403);
        }

        if (await _otpService.IsCooldownActiveAsync(normalizedEmail))
        {
            _logger.LogWarning("[AUTH] Resend OTP rejected: Cooldown is active for '{Email}'.", normalizedEmail);
            throw new DomainException("OTP_COOLDOWN_ACTIVE", "Please wait 60 seconds before requesting a new OTP.", 429);
        }

        await _otpService.InvalidateExistingOtpsAsync(normalizedEmail);

        var newOtpCode = Random.Shared.Next(100000, 999999).ToString();
        _logger.LogInformation("[OTP RESEND] Email: '{Email}' | NEW CODE: >>> {OtpCode} <<< | Valid: 5 mins", normalizedEmail, newOtpCode);
        await _otpService.SaveOtpAsync(normalizedEmail, newOtpCode, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15));
        await _otpService.SetCooldownAsync(normalizedEmail, TimeSpan.FromSeconds(60));

        if (_emailNotificationClient != null)
        {
            var fullName = $"{user.FirstName} {user.LastName}".Trim();
            var sent = await _emailNotificationClient.SendOtpEmailAsync(user.Email, fullName, newOtpCode, 5, cancellationToken);
            if (sent)
            {
                _logger.LogInformation("Dispatched resend OTP email via REST Notification client for '{Email}'.", normalizedEmail);
            }
            else
            {
                _logger.LogWarning("Failed to dispatch resend OTP email via REST Notification client. OTP fallback code: {Otp}", newOtpCode);
            }
        }
        else
        {
            _logger.LogWarning("[NOTIFICATION CLIENT OFFLINE] Simulated resending OTP email to '{Email}'. New OTP: {Otp}", normalizedEmail, newOtpCode);
        }

        // Commit changes to database
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    // =========================================================================================
    // SECTION 2: ĐĂNG NHẬP JWT VÀ VÒNG ĐỜI REFRESH TOKEN (JWT AUTHENTICATION & SESSION LIFECYCLE)
    // =========================================================================================

    /// <summary>
    /// Xác thực đăng nhập người dùng và cấp phát cặp Access Token (JWT) và Refresh Token.
    /// Kiểm tra mật khẩu (BCrypt), kiểm tra trạng thái khóa (Ban), cấp Access Token có claim vai trò đơn nhất.
    /// </summary>
    /// <param name="dto">Dữ liệu đăng nhập: Email/Username, Mật khẩu, DeviceInfo, IP.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu.</param>
    /// <returns>Token JWT và Refresh Token.</returns>
    public async Task<LoginResultDto> LoginAsync(LoginDto dto, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.UsernameOrEmail) || string.IsNullOrWhiteSpace(dto.Password))
        {
            throw new DomainException("INVALID_CREDENTIALS", "Invalid username, email, or password.", 401);
        }

        var identifier = dto.UsernameOrEmail.Trim();
        var normalizedIdentifier = identifier.ToLowerInvariant();

        var user = await _dbContext.Users
            .Include(u => u.Role)
                .ThenInclude(r => r!.ParentRole)
            .FirstOrDefaultAsync(u => u.Email == normalizedIdentifier || u.Email == identifier || (u.Username != null && u.Username.ToLower() == normalizedIdentifier), cancellationToken);

        if (user == null || user.IsDeleted)
        {
            _logger.LogWarning("[AUTH] Login failed: User '{Identifier}' does not exist or was deleted.", identifier);
            var attempts = await _otpService.IncrementLoginAttemptsAsync(normalizedIdentifier, TimeSpan.FromMinutes(30));
            if (attempts >= 5)
            {
                await _otpService.LockAccountAsync(normalizedIdentifier, TimeSpan.FromMinutes(30));
                throw new DomainException("ACCOUNT_LOCKED_TEMPORARY", "Account is temporarily locked for 30 minutes due to 5 consecutive failed login attempts.", 423);
            }
            throw new DomainException("INVALID_CREDENTIALS", "Invalid username, email, or password.", 401);
        }

        if (user.Status == UserStatus.Banned)
        {
            _logger.LogWarning("[AUTH] Login rejected: User '{Email}' has been banned. Reason: '{Reason}'.", user.Email, user.BanReason);
            throw new DomainException("ACCOUNT_BANNED", FormatBanMessage(user.BanReason), 403);
        }

        if (await _otpService.IsAccountLockedAsync(normalizedIdentifier))
        {
            _logger.LogWarning("[AUTH] Login rejected: Account '{Identifier}' is temporarily locked.", identifier);
            throw new DomainException("ACCOUNT_LOCKED_TEMPORARY", "Account is temporarily locked for 30 minutes due to 5 consecutive failed login attempts.", 423);
        }

        bool isPasswordValid = false;
        try
        {
            isPasswordValid = BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while verifying password hash for '{Email}'.", user.Email);
            isPasswordValid = false;
        }

        if (!isPasswordValid)
        {
            _logger.LogWarning("[AUTH] Login failed: Invalid password provided for '{Email}'.", user.Email);
            var attempts = await _otpService.IncrementLoginAttemptsAsync(normalizedIdentifier, TimeSpan.FromMinutes(30));
            if (attempts >= 5)
            {
                await _otpService.LockAccountAsync(normalizedIdentifier, TimeSpan.FromMinutes(30));
                throw new DomainException("ACCOUNT_LOCKED_TEMPORARY", "Account is temporarily locked for 30 minutes due to 5 consecutive failed login attempts.", 423);
            }
            throw new DomainException("INVALID_CREDENTIALS", "Invalid username, email, or password.", 401);
        }

        await _otpService.ResetLoginAttemptsAsync(normalizedIdentifier);

        if (user.Status == UserStatus.PendingVerification)
        {
            _logger.LogWarning("[AUTH] Login rejected: User '{Email}' is PendingVerification. OTP verification required.", user.Email);
            throw new DomainException("ACCOUNT_NOT_VERIFIED", "Account has not been verified with OTP.", 403);
        }

        var roles = new List<string>();
        ExtractUserRoles(user, roles);

        if (roles.Count == 0)
        {
            roles.Add("buyer");
        }

        if (_jwtTokenGenerator == null)
        {
            throw new InvalidOperationException("JwtTokenGenerator is not configured.");
        }

        var sessionId = Guid.NewGuid();
        var token = _jwtTokenGenerator.GenerateToken(user, roles, Array.Empty<string>(), sessionId, out var expiresAt);
        var expiresIn = (int)Math.Max(0, (expiresAt - DateTime.UtcNow).TotalSeconds);
        if (expiresIn == 0) expiresIn = 900;

        // Generate secure Refresh Token and store SHA-256 hash in database (TTL: 7 days)
        var rawRefreshToken = GenerateSecureToken();
        var tokenHash = ComputeSha256Hash(rawRefreshToken);

        var refreshTokenEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            SessionId = sessionId,
            TokenHash = tokenHash,
            DeviceInfo = Truncate(dto.DeviceInfo, 255),
            IpAddress = Truncate(dto.IpAddress, 100),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            IsRevoked = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.RefreshTokens.Add(refreshTokenEntity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[AUTH] User '{Email}' logged in successfully. UserId: {UserId} | Roles: [{Roles}]. JWT & RefreshToken issued.", user.Email, user.Id, string.Join(", ", roles));

        var primaryRole = roles.FirstOrDefault() ?? "buyer";
        var userResponse = new LoginUserApiResponse(
            Id: user.Id.ToString(),
            Email: user.Email,
            Username: user.Username,
            FirstName: user.FirstName,
            LastName: user.LastName,
            Roles: roles,
            Permissions: Array.Empty<string>(),
            UserId: user.Id.ToString(),
            Role: primaryRole
        );

        return new LoginResultDto(
            AccessToken: token,
            RefreshToken: rawRefreshToken,
            TokenType: "Bearer",
            ExpiresIn: expiresIn,
            User: userResponse,
            UserId: user.Id.ToString(),
            Email: user.Email,
            Role: primaryRole,
            Roles: roles,
            Permissions: Array.Empty<string>());
    }

    /// <summary>
    /// Xoay vòng Refresh Token (Token Rotation): Cấp mới Access Token và Refresh Token mới, thu hồi token cũ.
    /// Chống tấn công Replay Attack: Nếu phát hiện token đã thu hồi bị dùng lại, hủy toàn bộ phiên của user.
    /// </summary>
    /// <param name="dto">Refresh token cũ.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu.</param>
    /// <returns>Cặp token mới.</returns>
    public async Task<RefreshTokenResultDto> RefreshTokenAsync(RefreshTokenDto dto, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.RefreshToken))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Refresh token is required.");
        }

        var rawToken = dto.RefreshToken.Trim();
        var tokenHash = ComputeSha256Hash(rawToken);

        var tokenEntity = await _dbContext.RefreshTokens
            .Include(t => t.User)
                .ThenInclude(u => u!.Role)
                .ThenInclude(r => r!.ParentRole)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (tokenEntity == null)
        {
            _logger.LogWarning("[AUTH] Refresh token failed: Token hash not found in system.");
            throw new DomainException("INVALID_REFRESH_TOKEN", "Invalid refresh token.");
        }

        if (tokenEntity.IsRevoked)
        {
            _logger.LogWarning("[AUTH] Refresh token reuse detected: Token '{TokenId}' (Session '{SessionId}') has already been revoked.", tokenEntity.Id, tokenEntity.SessionId);
            if (tokenEntity.SessionId != Guid.Empty)
            {
                await _dbContext.RevokeSessionTokensAsync(tokenEntity.SessionId, DateTimeOffset.UtcNow, cancellationToken);
            }
            throw new DomainException("REFRESH_TOKEN_REVOKED", "Refresh token has been revoked.");
        }

        if (tokenEntity.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            _logger.LogWarning("[AUTH] Refresh token rejected: Token '{TokenId}' expired at {ExpiresAt}.", tokenEntity.Id, tokenEntity.ExpiresAt);
            throw new DomainException("REFRESH_TOKEN_EXPIRED", "Refresh token has expired.");
        }

        var user = tokenEntity.User;
        if (user == null || user.IsDeleted)
        {
            _logger.LogWarning("[AUTH] Refresh token rejected: User '{UserId}' not found or deleted.", tokenEntity.UserId);
            throw new DomainException("USER_NOT_FOUND", "User account not found.");
        }

        if (user.Status == UserStatus.Banned)
        {
            _logger.LogWarning("[AUTH] Refresh token rejected: User '{UserId}' is banned. Revoking token. Reason: '{Reason}'.", tokenEntity.UserId, user.BanReason);
            tokenEntity.IsRevoked = true;
            tokenEntity.RevokedAt = DateTimeOffset.UtcNow;
            tokenEntity.UpdatedAt = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
            var banMessage = !string.IsNullOrWhiteSpace(user.BanReason)
                ? $"User account has been banned. Reason: {user.BanReason.Trim()}."
                : "User account has been banned.";
            throw new DomainException("USER_BANNED", banMessage);
        }

        // --- Refresh Token Rotation (RTR) with Atomic Revoke & Reuse Detection ---
        var now = DateTimeOffset.UtcNow;
        var sessionId = tokenEntity.SessionId != Guid.Empty ? tokenEntity.SessionId : tokenEntity.Id;

        var wonAtomicRevoke = await _dbContext.TryRevokeRefreshTokenAtomicAsync(tokenEntity.Id, now, cancellationToken);
        if (!wonAtomicRevoke)
        {
            _logger.LogWarning("[AUTH] Concurrent or replayed refresh detected for Token '{TokenId}' (Session '{SessionId}'). Revoking session.", tokenEntity.Id, sessionId);
            await _dbContext.RevokeSessionTokensAsync(sessionId, now, cancellationToken);
            throw new DomainException("REFRESH_TOKEN_REVOKED", "Refresh token has been revoked.");
        }

        // 2. Generate new refresh token and persist to database (preserving SessionId)
        var newRawRefreshToken = GenerateSecureToken();
        var newTokenHash = ComputeSha256Hash(newRawRefreshToken);

        var newRefreshTokenEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            SessionId = sessionId,
            TokenHash = newTokenHash,
            DeviceInfo = Truncate(dto.DeviceInfo, 255) ?? tokenEntity.DeviceInfo,
            IpAddress = Truncate(dto.IpAddress, 100) ?? tokenEntity.IpAddress,
            ExpiresAt = now.AddDays(7),
            IsRevoked = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.RefreshTokens.Add(newRefreshTokenEntity);

        // 3. Extract latest active roles from DB
        var roles = new List<string>();
        ExtractUserRoles(user, roles);

        if (roles.Count == 0)
        {
            roles.Add("buyer");
        }

        if (_jwtTokenGenerator == null)
        {
            throw new InvalidOperationException("JwtTokenGenerator is not configured.");
        }

        // 4. Issue new access token with updated claims & stable sid
        var newAccessToken = _jwtTokenGenerator.GenerateToken(user, roles, Array.Empty<string>(), sessionId, out var expiresAt);
        var expiresIn = (int)Math.Max(0, (expiresAt - DateTime.UtcNow).TotalSeconds);
        if (expiresIn == 0) expiresIn = 900;

        var primaryRole = roles.FirstOrDefault() ?? "buyer";
        var userResponse = new LoginUserApiResponse(
            Id: user.Id.ToString(),
            Email: user.Email,
            Username: user.Username,
            FirstName: user.FirstName,
            LastName: user.LastName,
            Roles: roles,
            Permissions: Array.Empty<string>(),
            UserId: user.Id.ToString(),
            Role: primaryRole
        );

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[AUTH] Rotated refresh token for UserId: {UserId}. New JWT issued.", user.Id);

        return new RefreshTokenResultDto(
            AccessToken: newAccessToken,
            RefreshToken: newRawRefreshToken,
            ExpiresIn: expiresIn,
            UserId: user.Id.ToString(),
            Email: user.Email,
            Role: primaryRole,
            Roles: roles,
            Permissions: Array.Empty<string>(),
            User: userResponse);
    }

    /// <summary>
    /// Đăng xuất tài khoản khỏi thiết bị hiện tại (thu hồi Refresh Token tương ứng).
    /// </summary>
    /// <param name="refreshToken">Refresh token cần thu hồi.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu.</param>
    /// <returns>True nếu thu hồi thành công.</returns>
    public async Task<bool> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Refresh token is required.");
        }

        var rawToken = refreshToken.Trim();
        var tokenHash = ComputeSha256Hash(rawToken);

        var tokenEntity = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (tokenEntity == null)
        {
            _logger.LogWarning("[AUTH] Logout failed: Refresh token hash not found in system.");
            throw new DomainException("INVALID_REFRESH_TOKEN", "Invalid refresh token.");
        }

        // Idempotency: If token was already revoked, consider logout already successful
        if (tokenEntity.IsRevoked)
        {
            _logger.LogInformation("[AUTH] Logout idempotent: Token '{TokenId}' for UserId: {UserId} was already revoked.", tokenEntity.Id, tokenEntity.UserId);
            return true;
        }

        tokenEntity.IsRevoked = true;
        tokenEntity.RevokedAt = DateTimeOffset.UtcNow;
        tokenEntity.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[AUTH] User '{UserId}' logged out successfully. Refresh token '{TokenId}' revoked.", tokenEntity.UserId, tokenEntity.Id);
        return true;
    }

    /// <summary>
    /// Đăng xuất tài khoản khỏi tất cả các thiết bị (thu hồi toàn bộ Refresh Tokens của user).
    /// </summary>
    /// <param name="userId">ID người dùng cần đăng xuất toàn bộ.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu.</param>
    /// <returns>Số lượng phiên token đã thu hồi.</returns>
    public async Task<int> LogoutAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "User identifier is required.");
        }

        var activeTokens = await _dbContext.RefreshTokens
            .Where(t => t.UserId == userId && !t.IsRevoked)
            .ToListAsync(cancellationToken);

        if (activeTokens.Count > 0)
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var token in activeTokens)
            {
                token.IsRevoked = true;
                token.RevokedAt = now;
                token.UpdatedAt = now;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("[AUTH] User '{UserId}' logged out of all sessions. {Count} token(s) revoked.", userId, activeTokens.Count);
        return activeTokens.Count;
    }

    // =========================================================================================
    // SECTION 3: QUẢN LÝ VÀ ĐẶT LẠI MẬT KHẨU (PASSWORD MANAGEMENT)
    // =========================================================================================

    /// <summary>
    /// Yêu cầu đặt lại mật khẩu: Sinh OTP lưu Redis và gửi email khôi phục.
    /// </summary>
    /// <param name="dto">Email tài khoản yêu cầu.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu.</param>
    /// <returns>Xác nhận gửi OTP đặt lại mật khẩu.</returns>
    public async Task<ForgotPasswordResultDto> ForgotPasswordAsync(ForgotPasswordDto dto, CancellationToken cancellationToken = default)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.Email))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_EMAIL", "Email address is required.");
        }

        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
        if (!System.Text.RegularExpressions.Regex.IsMatch(normalizedEmail, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_EMAIL", "Email address format is invalid.");
        }

        const string genericMessage = "If the email exists in our system, password reset instructions have been sent.";
        const int otpExpiresInSeconds = 600;

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail && !u.IsDeleted, cancellationToken);

        if (user == null || user.Status == Domain.Enums.UserStatus.Banned)
        {
            _logger.LogInformation("[FORGOT PASSWORD] Requested for '{Email}', but user not eligible or not found (Anti-Enumeration).", normalizedEmail);
            return new ForgotPasswordResultDto(normalizedEmail, genericMessage, otpExpiresInSeconds);
        }

        if (await _otpService.IsCooldownActiveAsync(normalizedEmail, purpose: "password_reset"))
        {
            _logger.LogWarning("[FORGOT PASSWORD] Rejected: Cooldown is active for '{Email}'.", normalizedEmail);
            throw new DomainException("OTP_COOLDOWN_ACTIVE", "Please wait 60 seconds before requesting a new OTP.", 429);
        }

        var otpCode = Random.Shared.Next(100000, 999999).ToString();
        _logger.LogInformation("[PASSWORD RESET OTP] Email: '{Email}' | CODE: >>> {OtpCode} <<< | Valid: 10 mins", normalizedEmail, otpCode);

        await _otpService.InvalidateExistingOtpsAsync(normalizedEmail, purpose: "password_reset");
        await _otpService.SaveOtpAsync(normalizedEmail, otpCode, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(15), purpose: "password_reset");
        await _otpService.SetCooldownAsync(normalizedEmail, TimeSpan.FromSeconds(60), purpose: "password_reset");

        if (_emailNotificationClient != null)
        {
            try
            {
                var fullName = $"{user.FirstName} {user.LastName}".Trim();
                var sent = await _emailNotificationClient.SendOtpEmailAsync(user.Email, fullName, otpCode, 10, cancellationToken);
                if (sent)
                {
                    _logger.LogInformation("[FORGOT PASSWORD] Password reset OTP email dispatched via REST client for '{Email}'", normalizedEmail);
                }
                else
                {
                    _logger.LogWarning("[FORGOT PASSWORD] Failed to dispatch OTP email for '{Email}'. Fallback to log. CODE: >>> {OtpCode} <<<", normalizedEmail, otpCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[FORGOT PASSWORD CLIENT ERROR] Could not dispatch OTP email for '{Email}'. Fallback to log. CODE: >>> {OtpCode} <<<", normalizedEmail, otpCode);
            }
        }
        else
        {
            _logger.LogWarning("[FORGOT PASSWORD CLIENT OFFLINE] Simulated sending reset OTP email to '{Email}'. CODE: >>> {OtpCode} <<<", normalizedEmail, otpCode);
        }

        return new ForgotPasswordResultDto(normalizedEmail, genericMessage, otpExpiresInSeconds);
    }

    /// <summary>
    /// Đặt lại mật khẩu mới bằng mã OTP xác thực qua email.
    /// </summary>
    /// <param name="dto">Email, Mã OTP và Mật khẩu mới.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu.</param>
    /// <returns>Xác nhận đổi mật khẩu thành công.</returns>
    public async Task<ResetPasswordResultDto> ResetPasswordAsync(ResetPasswordDto dto, CancellationToken cancellationToken = default)
    {
        if (dto == null)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Reset password payload is required.");
        }

        // 1. Validate Email
        if (string.IsNullOrWhiteSpace(dto.Email))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_EMAIL", "Email address is required.");
        }
        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
        if (!System.Text.RegularExpressions.Regex.IsMatch(normalizedEmail, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_EMAIL", "Email address format is invalid.");
        }

        // 2. Validate OTP code
        if (string.IsNullOrWhiteSpace(dto.OtpCode) || dto.OtpCode.Trim().Length != 6 || !dto.OtpCode.Trim().All(char.IsDigit))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_OTP", "OTP code must be a 6-digit number.");
        }
        var trimmedOtp = dto.OtpCode.Trim();

        // 3. Validate NewPassword
        if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 8)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_PASSWORD", "Password must be at least 8 characters long.");
        }

        // 4. Validate ConfirmPassword
        if (dto.NewPassword != dto.ConfirmPassword)
        {
            throw new BusinessRuleValidationException("VAL_PASSWORD_MISMATCH", "New password and confirmation password do not match.");
        }

        // 5. Check User in database
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail && !u.IsDeleted, cancellationToken);

        if (user == null)
        {
            _logger.LogWarning("[RESET PASSWORD] User not found for '{Email}'.", normalizedEmail);
            throw new DomainException("USER_NOT_FOUND", "User account not found.");
        }

        if (user.Status == Domain.Enums.UserStatus.Banned)
        {
            _logger.LogWarning("[RESET PASSWORD] User '{Email}' is banned.", normalizedEmail);
            throw new DomainException("USER_BANNED", "User account is banned.");
        }

        // 6. Verify OTP from Redis
        var otpEntry = await _otpService.GetOtpAsync(normalizedEmail, purpose: "password_reset");
        if (otpEntry == null || otpEntry.Status == OtpStatus.Expired || DateTimeOffset.UtcNow > otpEntry.ExpiresAt)
        {
            _logger.LogWarning("[RESET PASSWORD] OTP expired or not found for '{Email}'.", normalizedEmail);
            throw new DomainException("OTP_EXPIRED", "OTP code has expired or does not exist.");
        }

        if (otpEntry.Status == OtpStatus.Consumed)
        {
            _logger.LogWarning("[RESET PASSWORD] OTP already consumed for '{Email}'.", normalizedEmail);
            throw new DomainException("OTP_ALREADY_USED", "This OTP code has already been used.");
        }

        if (otpEntry.Code != trimmedOtp)
        {
            var attempts = await _otpService.IncrementOtpAttemptsAsync(normalizedEmail, TimeSpan.FromMinutes(10), purpose: "password_reset");
            _logger.LogWarning("[RESET PASSWORD] Invalid OTP provided for '{Email}'. Attempt {Attempts}/5.", normalizedEmail, attempts);

            if (attempts >= 5)
            {
                await _otpService.InvalidateExistingOtpsAsync(normalizedEmail, purpose: "password_reset");
                _logger.LogWarning("[RESET PASSWORD] Maximum OTP verification attempts exceeded for '{Email}'. Invalidated reset OTP.", normalizedEmail);
                throw new DomainException("OTP_MAX_ATTEMPTS_EXCEEDED", "Too many incorrect OTP attempts. The OTP code has been invalidated. Please request a new code.");
            }

            throw new DomainException("OTP_INVALID", "OTP code is invalid.");
        }

        // 7. Reset attempts and Mark OTP as consumed
        await _otpService.ResetOtpAttemptsAsync(normalizedEmail, purpose: "password_reset");
        await _otpService.MarkOtpConsumedAsync(normalizedEmail, purpose: "password_reset");

        // 8. Hash new password with BCrypt
        var newHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        user.PasswordHash = newHash;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        // 9. Revoke all active refresh tokens for security
        var activeTokens = await _dbContext.RefreshTokens
            .Where(t => t.UserId == user.Id && !t.IsRevoked)
            .ToListAsync(cancellationToken);

        if (activeTokens.Count > 0)
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var token in activeTokens)
            {
                token.IsRevoked = true;
                token.RevokedAt = now;
                token.UpdatedAt = now;
            }
            _logger.LogInformation("[RESET PASSWORD] Revoked {Count} active session(s) for '{Email}'.", activeTokens.Count, normalizedEmail);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[RESET PASSWORD] Password reset successfully for '{Email}' (UserId: {UserId}).", normalizedEmail, user.Id);
        return new ResetPasswordResultDto(true, "Password has been reset successfully.");
    }

    /// <summary>
    /// Đổi mật khẩu cho người dùng đã đăng nhập (xác thực mật khẩu cũ bằng BCrypt).
    /// </summary>
    /// <param name="userId">ID người dùng đang đăng nhập.</param>
    /// <param name="dto">Mật khẩu cũ và Mật khẩu mới.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu.</param>
    /// <returns>Xác nhận đổi mật khẩu thành công.</returns>
    public async Task<ChangePasswordResultDto> ChangePasswordAsync(Guid userId, ChangePasswordDto dto, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "User identifier is required.");
        }

        if (dto == null)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Change password payload is required.");
        }

        // 1. Validate OldPassword
        if (string.IsNullOrWhiteSpace(dto.OldPassword))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_PASSWORD", "Old password is required.");
        }

        // 2. Validate NewPassword
        if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 8)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_PASSWORD", "Password must be at least 8 characters long.");
        }

        // 3. Validate ConfirmPassword
        if (dto.NewPassword != dto.ConfirmPassword)
        {
            throw new BusinessRuleValidationException("VAL_PASSWORD_MISMATCH", "New password and confirmation password do not match.");
        }

        // 4. Find user in database
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, cancellationToken);

        if (user == null)
        {
            _logger.LogWarning("[CHANGE PASSWORD] User '{UserId}' not found.", userId);
            throw new DomainException("USER_NOT_FOUND", "User account not found.");
        }

        if (user.Status == Domain.Enums.UserStatus.Banned)
        {
            _logger.LogWarning("[CHANGE PASSWORD] User '{UserId}' is banned.", userId);
            throw new DomainException("USER_BANNED", "User account is banned.");
        }

        // 5. Verify OldPassword with BCrypt
        if (!BCrypt.Net.BCrypt.Verify(dto.OldPassword, user.PasswordHash))
        {
            _logger.LogWarning("[CHANGE PASSWORD] Old password verification failed for user '{UserId}'.", userId);
            throw new DomainException("INVALID_CREDENTIALS", "Old password is incorrect.");
        }

        // 6. Check that NewPassword is not the same as OldPassword
        if (string.Equals(dto.OldPassword, dto.NewPassword, StringComparison.Ordinal) ||
            BCrypt.Net.BCrypt.Verify(dto.NewPassword, user.PasswordHash))
        {
            _logger.LogWarning("[CHANGE PASSWORD] New password is the same as the old password for user '{UserId}'.", userId);
            throw new BusinessRuleValidationException("VAL_SAME_PASSWORD", "New password cannot be the same as the old password.");
        }

        // 7. Hash new password with BCrypt
        var newHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        user.PasswordHash = newHash;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        // 8. Revoke all active refresh tokens for security
        var activeTokens = await _dbContext.RefreshTokens
            .Where(t => t.UserId == user.Id && !t.IsRevoked)
            .ToListAsync(cancellationToken);

        if (activeTokens.Count > 0)
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var token in activeTokens)
            {
                token.IsRevoked = true;
                token.RevokedAt = now;
                token.UpdatedAt = now;
            }
            _logger.LogInformation("[CHANGE PASSWORD] Revoked {Count} active session(s) for user '{UserId}'.", activeTokens.Count, userId);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[CHANGE PASSWORD] Password changed successfully for user '{UserId}'.", userId);
        return new ChangePasswordResultDto(true, "Password changed successfully.");
    }

    // =========================================================================================
    // SECTION 4: HÀM PHỤ TRỢ MÃ HÓA VÀ BẢO MẬT (PRIVATE SECURITY HELPERS)
    // =========================================================================================

    private static string GenerateSecureToken()
    {
        var bytes = new byte[64];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    private static string ComputeSha256Hash(string input)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        var hashBytes = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static string FormatBanMessage(string? banReason)
    {
        return !string.IsNullOrWhiteSpace(banReason)
            ? $"Account has been locked or banned by an administrator. Reason: {banReason.Trim()}."
            : "Account has been locked or banned by an administrator.";
    }

    private static void ExtractUserRoles(User user, List<string> roles)
    {
        var currentRole = user.Role;
        while (currentRole != null)
        {
            var roleClaim = currentRole.RoleCode switch
            {
                RoleType.Admin => "admin",
                RoleType.SupportStaff => "staff",
                RoleType.Seller => "seller",
                RoleType.Buyer => "buyer",
                _ => currentRole.RoleCode.ToString().ToLowerInvariant()
            };

            if (!roles.Contains(roleClaim))
            {
                roles.Add(roleClaim);
            }

            var roleName = currentRole.RoleName?.Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(roleName) && !roles.Contains(roleName))
            {
                roles.Add(roleName);
            }

            currentRole = currentRole.ParentRole;
        }

        if (roles.Count == 0)
        {
            roles.Add("buyer");
        }
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
