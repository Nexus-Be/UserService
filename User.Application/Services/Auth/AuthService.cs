namespace User.Application.Services.Auth;

using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexus.Contracts.Events;
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
    private readonly IPublishEndpoint? _publishEndpoint;
    private readonly ILogger<AuthService> _logger;

    private static readonly Guid BuyerRoleId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public AuthService(
        IUserDbContext dbContext,
        IRedisOtpService otpService,
        ILogger<AuthService> logger,
        IJwtTokenGenerator? jwtTokenGenerator = null,
        IPublishEndpoint? publishEndpoint = null)
    {
        _dbContext = dbContext;
        _otpService = otpService;
        _logger = logger;
        _jwtTokenGenerator = jwtTokenGenerator;
        _publishEndpoint = publishEndpoint;
    }

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
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        User user;

        if (existingUser != null)
        {
            if (existingUser.Status == UserStatus.Active)
            {
                throw new DomainException("EMAIL_ALREADY_EXISTS", "Email is already registered in the system.");
            }

            // Trường hợp đăng ký lại khi tài khoản chưa xác nhận (PendingVerification):
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
            // Tạo mới User với trạng thái PendingVerification
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

            // Gán role mặc định: Buyer
            var buyerRole = await _dbContext.Roles.FirstOrDefaultAsync(r => r.RoleCode == RoleType.Buyer, cancellationToken);
            var roleId = buyerRole?.Id ?? BuyerRoleId;

            _dbContext.UserRoles.Add(new UserRole
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                RoleId = roleId,
                AssignedAt = DateTimeOffset.UtcNow
            });

            // Tạo bảng điểm uy tín mặc định (Standard: 50 điểm)
            _dbContext.ReputationScores.Add(new ReputationScore
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CurrentScore = 50,
                TrustLevel = TrustLevel.Standard
            });

            _logger.LogInformation("New user registration initiated for '{Email}' with PendingVerification status and Buyer role.", email);
        }

        // Sinh mã OTP 6 số ngẫu nhiên
        var otpCode = Random.Shared.Next(100000, 999999).ToString();
        _logger.LogInformation("[OTP DISPATCH] Email: '{Email}' | CODE: >>> {OtpCode} <<< | Valid: 5 mins", email, otpCode);

        // Lưu vào Redis: Hiệu lực 5 phút, lưu trữ tối đa 15 phút
        await _otpService.SaveOtpAsync(email, otpCode, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15));

        // Lưu CSDL
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Bắn sự kiện lên RabbitMQ cho Worker gửi mail bất đồng bộ
        if (_publishEndpoint != null)
        {
            try
            {
                var fullName = $"{user.FirstName} {user.LastName}".Trim();
                await _publishEndpoint.Publish(new SendOtpEmailIntegrationEvent(user.Email, fullName, otpCode, 5), cancellationToken);
                _logger.LogInformation("Published SendOtpEmailIntegrationEvent for recipient '{Email}'.", email);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish SendOtpEmailIntegrationEvent to RabbitMQ. OTP simulation code: {Otp}", otpCode);
            }
        }
        else
        {
            _logger.LogWarning("[MESSAGE BUS OFFLINE] Simulated sending OTP email to '{Email}'. OTP: {Otp}", email, otpCode);
        }

        return user;
    }

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
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (user == null)
        {
            throw new DomainException("USER_NOT_FOUND", "User account not found.");
        }

        if (user.Status == UserStatus.Active)
        {
            return user;
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
            _logger.LogWarning("[AUTH] OTP verification failed for '{Email}': Provided '{Provided}' does not match expected OTP.", normalizedEmail, otp.Trim());
            throw new DomainException("OTP_INVALID", "OTP code is invalid.");
        }

        await _otpService.MarkOtpConsumedAsync(normalizedEmail);

        user.Status = UserStatus.Active;
        user.EmailVerifiedAt = DateTimeOffset.UtcNow;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("[AUTH] User '{Email}' successfully verified OTP. Status changed to Active.", normalizedEmail);

        return user;
    }

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

        await _otpService.InvalidateExistingOtpsAsync(normalizedEmail);

        var newOtpCode = Random.Shared.Next(100000, 999999).ToString();
        _logger.LogInformation("[OTP RESEND] Email: '{Email}' | NEW CODE: >>> {OtpCode} <<< | Valid: 5 mins", normalizedEmail, newOtpCode);
        await _otpService.SaveOtpAsync(normalizedEmail, newOtpCode, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15));

        if (_publishEndpoint != null)
        {
            try
            {
                var fullName = $"{user.FirstName} {user.LastName}".Trim();
                await _publishEndpoint.Publish(new SendOtpEmailIntegrationEvent(user.Email, fullName, newOtpCode, 5), cancellationToken);
                _logger.LogInformation("Published resend SendOtpEmailIntegrationEvent for '{Email}'.", normalizedEmail);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish resend SendOtpEmailIntegrationEvent to RabbitMQ. OTP simulation code: {Otp}", newOtpCode);
            }
        }
        else
        {
            _logger.LogWarning("[MESSAGE BUS OFFLINE] Simulated resending OTP email to '{Email}'. New OTP: {Otp}", normalizedEmail, newOtpCode);
        }

        return true;
    }

    public async Task<LoginResultDto> LoginAsync(LoginDto dto, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.UsernameOrEmail) || string.IsNullOrWhiteSpace(dto.Password))
        {
            throw new DomainException("INVALID_CREDENTIALS", "Invalid username or password.");
        }

        var identifier = dto.UsernameOrEmail.Trim();
        var normalizedIdentifier = identifier.ToLowerInvariant();

        var user = await _dbContext.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email == normalizedIdentifier || u.Email == identifier || (u.Username != null && u.Username.ToLower() == normalizedIdentifier), cancellationToken);

        if (user == null || user.IsDeleted)
        {
            _logger.LogWarning("[AUTH] Login failed: User '{Identifier}' does not exist or was deleted.", identifier);
            throw new DomainException("INVALID_CREDENTIALS", "Invalid username or password.");
        }

        if (user.Status == UserStatus.PendingVerification)
        {
            _logger.LogWarning("[AUTH] Login rejected: User '{Email}' is PendingVerification. OTP verification required.", user.Email);
            throw new DomainException("USER_NOT_VERIFIED", "User account is pending verification. Please verify your OTP code.");
        }

        if (user.Status == UserStatus.Banned)
        {
            _logger.LogWarning("[AUTH] Login rejected: User '{Email}' has been banned.", user.Email);
            throw new DomainException("USER_BANNED", "User account has been banned.");
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
            throw new DomainException("INVALID_CREDENTIALS", "Invalid username or password.");
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

        if (_jwtTokenGenerator == null)
        {
            throw new InvalidOperationException("JwtTokenGenerator is not configured.");
        }

        var token = _jwtTokenGenerator.GenerateToken(user, roles, out var expiresAt);

        // Sinh Refresh Token an toàn và lưu bản băm SHA-256 vào Database (TTL: 7 ngày)
        var rawRefreshToken = GenerateSecureToken();
        var tokenHash = ComputeSha256Hash(rawRefreshToken);

        var refreshTokenEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            IsRevoked = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.RefreshTokens.Add(refreshTokenEntity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[AUTH] User '{Email}' logged in successfully. UserId: {UserId} | Roles: [{Roles}]. JWT & RefreshToken issued.", user.Email, user.Id, string.Join(", ", roles));

        return new LoginResultDto(
            AccessToken: token,
            ExpiresAt: expiresAt.ToString("o"),
            UserId: user.Id.ToString(),
            Roles: roles,
            RefreshToken: rawRefreshToken);
    }

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
                .ThenInclude(u => u!.UserRoles)
                    .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (tokenEntity == null)
        {
            _logger.LogWarning("[AUTH] Refresh token failed: Token hash not found in system.");
            throw new DomainException("INVALID_REFRESH_TOKEN", "Invalid refresh token.");
        }

        if (tokenEntity.IsRevoked)
        {
            _logger.LogWarning("[AUTH] Refresh token rejected: Token '{TokenId}' has already been revoked.", tokenEntity.Id);
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
            _logger.LogWarning("[AUTH] Refresh token rejected: User '{UserId}' is banned. Revoking token.", tokenEntity.UserId);
            tokenEntity.IsRevoked = true;
            tokenEntity.RevokedAt = DateTimeOffset.UtcNow;
            tokenEntity.UpdatedAt = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
            throw new DomainException("USER_BANNED", "User account has been banned.");
        }

        // --- Refresh Token Rotation (RTR) ---
        // 1. Thu hồi refresh token hiện tại
        tokenEntity.IsRevoked = true;
        tokenEntity.RevokedAt = DateTimeOffset.UtcNow;
        tokenEntity.UpdatedAt = DateTimeOffset.UtcNow;

        // 2. Sinh refresh token mới và lưu vào Database
        var newRawRefreshToken = GenerateSecureToken();
        var newTokenHash = ComputeSha256Hash(newRawRefreshToken);

        var newRefreshTokenEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = newTokenHash,
            DeviceInfo = dto.DeviceInfo ?? tokenEntity.DeviceInfo,
            IpAddress = dto.IpAddress ?? tokenEntity.IpAddress,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            IsRevoked = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.RefreshTokens.Add(newRefreshTokenEntity);

        // 3. Trích xuất vai trò hiện tại (Roles) mới nhất từ DB
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

        if (_jwtTokenGenerator == null)
        {
            throw new InvalidOperationException("JwtTokenGenerator is not configured.");
        }

        // 4. Phát hành Access Token mới mang quyền hạn cập nhật
        var newAccessToken = _jwtTokenGenerator.GenerateToken(user, roles, out var expiresAt);

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[AUTH] Rotated refresh token for UserId: {UserId}. New JWT issued.", user.Id);

        return new RefreshTokenResultDto(
            AccessToken: newAccessToken,
            RefreshToken: newRawRefreshToken,
            ExpiresAt: expiresAt.ToString("o"),
            UserId: user.Id.ToString(),
            Roles: roles);
    }

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

        var otpCode = Random.Shared.Next(100000, 999999).ToString();
        _logger.LogInformation("[PASSWORD RESET OTP] Email: '{Email}' | CODE: >>> {OtpCode} <<< | Valid: 10 mins", normalizedEmail, otpCode);

        await _otpService.InvalidateExistingOtpsAsync(normalizedEmail, purpose: "password_reset");
        await _otpService.SaveOtpAsync(normalizedEmail, otpCode, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(15), purpose: "password_reset");

        if (_publishEndpoint != null)
        {
            try
            {
                var fullName = $"{user.FirstName} {user.LastName}".Trim();
                await _publishEndpoint.Publish(new SendOtpEmailIntegrationEvent(user.Email, fullName, otpCode, 10), cancellationToken);
                _logger.LogInformation("[FORGOT PASSWORD] Password reset OTP event dispatched to RabbitMQ for '{Email}'", normalizedEmail);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[FORGOT PASSWORD BUS OFFLINE] Could not publish SendOtpEmailIntegrationEvent for '{Email}'. Fallback to log. CODE: >>> {OtpCode} <<<", normalizedEmail, otpCode);
            }
        }

        return new ForgotPasswordResultDto(normalizedEmail, genericMessage, otpExpiresInSeconds);
    }

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
}
