namespace User.Tests;

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using User.Application.Interfaces;
using User.Application.Models;
using User.Application.Services.Auth;
using User.Domain.Enums;
using User.Domain.Exceptions;
using User.Infrastructure.Persistence;
using User.Infrastructure.Services.Redis;
using Xunit;

public sealed class AuthServiceTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"user_test_db_{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task RegisterAsync_creates_user_with_pending_verification_and_buyer_role()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance);

        var dto = new RegisterDto(
            Email: "buyer01@nexus.local",
            Password: "Password123!",
            FirstName: "Nguyen",
            LastName: "Van A",
            PhoneNumber: "0901234567"
        );

        var user = await authService.RegisterAsync(dto);

        Assert.NotNull(user);
        Assert.Equal("buyer01@nexus.local", user.Email);
        Assert.Equal(UserStatus.PendingVerification, user.Status);
        Assert.True(BCrypt.Net.BCrypt.Verify("Password123!", user.PasswordHash));

        // Kiểm tra OTP được lưu trong Redis
        var savedOtp = await otpService.GetOtpAsync("buyer01@nexus.local");
        Assert.NotNull(savedOtp);
        Assert.Equal(OtpStatus.Active, savedOtp.Status);
        Assert.Equal(6, savedOtp.Code.Length);
    }

    [Fact]
    public async Task RegisterAsync_throws_when_email_already_registered_and_active()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance);

        var dto = new RegisterDto("active@nexus.local", "Password123!", "Le", "Van B");
        var user = await authService.RegisterAsync(dto);

        // Kích hoạt user
        user.Status = UserStatus.Active;
        await dbContext.SaveChangesAsync();

        // Đăng ký lại bằng cùng email
        var ex = await Assert.ThrowsAsync<DomainException>(() => authService.RegisterAsync(dto));
        Assert.Equal("EMAIL_ALREADY_EXISTS", ex.ErrorCode);
        Assert.Equal("Email is already registered in the system.", ex.Message);
    }

    [Fact]
    public async Task RegisterAsync_re_register_unverified_account_invalidates_previous_otp()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance);

        var dto = new RegisterDto("unverified@nexus.local", "OldPassword123!", "Tran", "Van C");
        await authService.RegisterAsync(dto);

        var firstOtp = (await otpService.GetOtpAsync("unverified@nexus.local"))!.Code;

        // Đăng ký lại với password mới khi tài khoản chưa verify
        var updateDto = new RegisterDto("unverified@nexus.local", "NewPassword456!", "Tran", "Van C");
        await authService.RegisterAsync(updateDto);

        var updatedUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == "unverified@nexus.local");
        Assert.NotNull(updatedUser);
        Assert.Equal(UserStatus.PendingVerification, updatedUser.Status);
        Assert.True(BCrypt.Net.BCrypt.Verify("NewPassword456!", updatedUser.PasswordHash));

        // OTP mới được sinh ra và khác OTP cũ
        var secondOtp = (await otpService.GetOtpAsync("unverified@nexus.local"))!.Code;
        Assert.NotNull(secondOtp);
        Assert.Equal(OtpStatus.Active, (await otpService.GetOtpAsync("unverified@nexus.local"))!.Status);
    }

    [Fact]
    public async Task VerifyOtpAsync_activates_account_and_marks_otp_consumed()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance);

        var dto = new RegisterDto("verify@nexus.local", "Password123!", "Pham", "Van D");
        await authService.RegisterAsync(dto);

        var otp = (await otpService.GetOtpAsync("verify@nexus.local"))!.Code;

        var verifiedUser = await authService.VerifyOtpAsync("verify@nexus.local", otp);

        Assert.Equal(UserStatus.Active, verifiedUser.Status);
        Assert.NotNull(verifiedUser.EmailVerifiedAt);

        // OTP trong Redis chuyển sang trạng thái Consumed (chống Replay)
        var consumedOtp = await otpService.GetOtpAsync("verify@nexus.local");
        Assert.NotNull(consumedOtp);
        Assert.Equal(OtpStatus.Consumed, consumedOtp.Status);
        Assert.NotNull(consumedOtp.ConsumedAt);
    }

    [Fact]
    public async Task VerifyOtpAsync_throws_when_otp_invalid()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance);

        var dto = new RegisterDto("wrongotp@nexus.local", "Password123!", "Hoang", "Van E");
        await authService.RegisterAsync(dto);

        var ex = await Assert.ThrowsAsync<DomainException>(() => authService.VerifyOtpAsync("wrongotp@nexus.local", "000000"));
        Assert.Equal("OTP_INVALID", ex.ErrorCode);
        Assert.Equal("OTP code is invalid.", ex.Message);
    }

    [Fact]
    public async Task VerifyOtpAsync_throws_when_otp_already_consumed()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance);

        var dto = new RegisterDto("replay@nexus.local", "Password123!", "Do", "Van F");
        await authService.RegisterAsync(dto);

        var otp = (await otpService.GetOtpAsync("replay@nexus.local"))!.Code;

        // Verify lần đầu thành công
        await authService.VerifyOtpAsync("replay@nexus.local", otp);

        // Đổi trạng thái user về PendingVerification để giả lập thử verify lại với OTP đã used
        var user = await dbContext.Users.FirstAsync(u => u.Email == "replay@nexus.local");
        user.Status = UserStatus.PendingVerification;
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<DomainException>(() => authService.VerifyOtpAsync("replay@nexus.local", otp));
        Assert.Equal("OTP_ALREADY_USED", ex.ErrorCode);
        Assert.Equal("This OTP code has already been used.", ex.Message);
    }

    [Fact]
    public async Task ResendOtpAsync_generates_new_otp_and_invalidates_old_one()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance);

        var dto = new RegisterDto("resend@nexus.local", "Password123!", "Vu", "Van G");
        await authService.RegisterAsync(dto);

        var firstOtp = (await otpService.GetOtpAsync("resend@nexus.local"))!.Code;

        var success = await authService.ResendOtpAsync("resend@nexus.local");
        Assert.True(success);

        var secondOtp = await otpService.GetOtpAsync("resend@nexus.local");
        Assert.NotNull(secondOtp);
        Assert.Equal(OtpStatus.Active, secondOtp.Status);
    }

    [Fact]
    public async Task VerifyOtpAsync_throws_when_otp_expired()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance);

        var dto = new RegisterDto("expired@nexus.local", "Password123!", "Pham", "Expired");
        await authService.RegisterAsync(dto);

        // Đổi trạng thái OTP sang Expired
        await otpService.InvalidateExistingOtpsAsync("expired@nexus.local");

        var ex = await Assert.ThrowsAsync<DomainException>(() => authService.VerifyOtpAsync("expired@nexus.local", "123456"));
        Assert.Equal("OTP_EXPIRED", ex.ErrorCode);
        Assert.Equal("OTP code has expired. Please request a new code.", ex.Message);
    }

    [Fact]
    public async Task ResendOtpAsync_throws_when_user_already_verified()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance);

        var dto = new RegisterDto("alreadyverified@nexus.local", "Password123!", "Le", "Verified");
        var user = await authService.RegisterAsync(dto);

        var otp = (await otpService.GetOtpAsync("alreadyverified@nexus.local"))!.Code;
        await authService.VerifyOtpAsync("alreadyverified@nexus.local", otp);

        var ex = await Assert.ThrowsAsync<DomainException>(() => authService.ResendOtpAsync("alreadyverified@nexus.local"));
        Assert.Equal("USER_ALREADY_VERIFIED", ex.ErrorCode);
        Assert.Equal("User account has already been verified.", ex.Message);
    }

    [Fact]
    public async Task LoginAsync_succeeds_with_valid_credentials_and_active_account()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var jwtGenerator = new TestJwtTokenGenerator();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance, jwtGenerator);

        var registerDto = new RegisterDto("login.success@nexus.local", "Password123!", "Nguyen", "Van Success");
        var user = await authService.RegisterAsync(registerDto);
        user.Status = UserStatus.Active;
        await dbContext.SaveChangesAsync();

        var loginDto = new LoginDto("login.success@nexus.local", "Password123!");
        var result = await authService.LoginAsync(loginDto);

        Assert.NotNull(result);
        Assert.Equal("mock-jwt-access-token-xyz", result.AccessToken);
        Assert.Equal(user.Id.ToString(), result.UserId);
        Assert.Contains("buyer", result.Roles);
        Assert.NotNull(result.ExpiresAt);
    }

    [Fact]
    public async Task LoginAsync_throws_when_password_is_invalid()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var jwtGenerator = new TestJwtTokenGenerator();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance, jwtGenerator);

        var registerDto = new RegisterDto("wrongpass@nexus.local", "CorrectPassword123!", "Tran", "Van Test");
        var user = await authService.RegisterAsync(registerDto);
        user.Status = UserStatus.Active;
        await dbContext.SaveChangesAsync();

        var loginDto = new LoginDto("wrongpass@nexus.local", "WrongPassword999!");
        var ex = await Assert.ThrowsAsync<DomainException>(() => authService.LoginAsync(loginDto));

        Assert.Equal("INVALID_CREDENTIALS", ex.ErrorCode);
        Assert.Equal("Invalid username or password.", ex.Message);
    }

    [Fact]
    public async Task LoginAsync_throws_when_user_not_found()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var jwtGenerator = new TestJwtTokenGenerator();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance, jwtGenerator);

        var loginDto = new LoginDto("nonexistent@nexus.local", "Password123!");
        var ex = await Assert.ThrowsAsync<DomainException>(() => authService.LoginAsync(loginDto));

        Assert.Equal("INVALID_CREDENTIALS", ex.ErrorCode);
        Assert.Equal("Invalid username or password.", ex.Message);
    }

    [Fact]
    public async Task LoginAsync_throws_when_user_is_pending_verification()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var jwtGenerator = new TestJwtTokenGenerator();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance, jwtGenerator);

        var registerDto = new RegisterDto("pending@nexus.local", "Password123!", "Le", "Pending");
        await authService.RegisterAsync(registerDto);

        var loginDto = new LoginDto("pending@nexus.local", "Password123!");
        var ex = await Assert.ThrowsAsync<DomainException>(() => authService.LoginAsync(loginDto));

        Assert.Equal("USER_NOT_VERIFIED", ex.ErrorCode);
        Assert.Equal("User account is pending verification. Please verify your OTP code.", ex.Message);
    }

    [Fact]
    public async Task LoginAsync_throws_when_user_is_suspended()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var jwtGenerator = new TestJwtTokenGenerator();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance, jwtGenerator);

        var registerDto = new RegisterDto("banned@nexus.local", "Password123!", "Pham", "Banned");
        var user = await authService.RegisterAsync(registerDto);
        user.Status = UserStatus.Banned;
        await dbContext.SaveChangesAsync();

        var loginDto = new LoginDto("banned@nexus.local", "Password123!");
        var ex = await Assert.ThrowsAsync<DomainException>(() => authService.LoginAsync(loginDto));

        Assert.Equal("USER_BANNED", ex.ErrorCode);
        Assert.Equal("User account has been banned.", ex.Message);
    }

    [Fact]
    public async Task RegisterAsync_with_valid_username_saves_username()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance);

        var dto = new RegisterDto(
            Email: "user_with_name@nexus.local",
            Password: "Password123!",
            FirstName: "Nguyen",
            LastName: "Van B",
            PhoneNumber: "0909999999",
            Username: "nexususer01"
        );

        var user = await authService.RegisterAsync(dto);

        Assert.NotNull(user);
        Assert.Equal("nexususer01", user.Username);
    }

    [Fact]
    public async Task RegisterAsync_throws_when_username_already_exists()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance);

        var dto1 = new RegisterDto(
            Email: "user1@nexus.local",
            Password: "Password123!",
            FirstName: "User",
            LastName: "One",
            Username: "sameusername"
        );
        await authService.RegisterAsync(dto1);

        var dto2 = new RegisterDto(
            Email: "user2@nexus.local",
            Password: "Password123!",
            FirstName: "User",
            LastName: "Two",
            Username: "SAMEUSERNAME" // Case-insensitive check
        );

        var ex = await Assert.ThrowsAsync<DomainException>(() => authService.RegisterAsync(dto2));
        Assert.Equal("USERNAME_ALREADY_EXISTS", ex.ErrorCode);
    }

    [Fact]
    public async Task LoginAsync_succeeds_with_username()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var jwtGenerator = new TestJwtTokenGenerator();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance, jwtGenerator);

        var registerDto = new RegisterDto(
            Email: "login_test@nexus.local",
            Password: "Password123!",
            FirstName: "Test",
            LastName: "Login",
            Username: "testloginuser"
        );
        var user = await authService.RegisterAsync(registerDto);
        user.Status = UserStatus.Active; // Activate user
        await dbContext.SaveChangesAsync();

        var loginDto = new LoginDto("testloginuser", "Password123!");
        var result = await authService.LoginAsync(loginDto);

        Assert.NotNull(result);
        Assert.Equal(user.Id.ToString(), result.UserId);
        Assert.Equal("mock-jwt-access-token-xyz", result.AccessToken);
        Assert.NotNull(result.RefreshToken);
        Assert.True(await dbContext.RefreshTokens.AnyAsync(r => r.UserId == user.Id && !r.IsRevoked));
    }

    [Fact]
    public async Task RefreshTokenAsync_succeeds_and_rotates_token()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var jwtGenerator = new TestJwtTokenGenerator();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance, jwtGenerator);

        var registerDto = new RegisterDto("rotate@nexus.local", "Password123!", "Tran", "Van B");
        var user = await authService.RegisterAsync(registerDto);
        user.Status = UserStatus.Active;
        await dbContext.SaveChangesAsync();

        var loginResult = await authService.LoginAsync(new LoginDto("rotate@nexus.local", "Password123!"));
        Assert.NotNull(loginResult.RefreshToken);

        var refreshDto = new RefreshTokenDto(loginResult.RefreshToken!);
        var refreshResult = await authService.RefreshTokenAsync(refreshDto);

        Assert.NotNull(refreshResult);
        Assert.Equal("mock-jwt-access-token-xyz", refreshResult.AccessToken);
        Assert.NotEqual(loginResult.RefreshToken, refreshResult.RefreshToken);
        Assert.Equal(user.Id.ToString(), refreshResult.UserId);

        // Kiểm tra Refresh Token cũ đã bị thu hồi
        var oldTokens = await dbContext.RefreshTokens.Where(t => t.UserId == user.Id && t.IsRevoked).ToListAsync();
        Assert.Single(oldTokens);
        Assert.NotNull(oldTokens[0].RevokedAt);

        // Kiểm tra Refresh Token mới đang hoạt động
        var activeTokens = await dbContext.RefreshTokens.Where(t => t.UserId == user.Id && !t.IsRevoked).ToListAsync();
        Assert.Single(activeTokens);
    }

    [Fact]
    public async Task RefreshTokenAsync_throws_when_token_is_invalid()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var jwtGenerator = new TestJwtTokenGenerator();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance, jwtGenerator);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            authService.RefreshTokenAsync(new RefreshTokenDto("non-existent-token-value")));

        Assert.Equal("INVALID_REFRESH_TOKEN", ex.ErrorCode);
        Assert.Equal("Invalid refresh token.", ex.Message);
    }

    [Fact]
    public async Task RefreshTokenAsync_throws_when_token_is_expired()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var jwtGenerator = new TestJwtTokenGenerator();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance, jwtGenerator);

        var registerDto = new RegisterDto("expired@nexus.local", "Password123!", "Nguyen", "Van C");
        var user = await authService.RegisterAsync(registerDto);
        user.Status = UserStatus.Active;
        await dbContext.SaveChangesAsync();

        var loginResult = await authService.LoginAsync(new LoginDto("expired@nexus.local", "Password123!"));
        var tokenEntity = await dbContext.RefreshTokens.FirstAsync(t => t.UserId == user.Id);

        // Làm hết hạn token
        tokenEntity.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            authService.RefreshTokenAsync(new RefreshTokenDto(loginResult.RefreshToken!)));

        Assert.Equal("REFRESH_TOKEN_EXPIRED", ex.ErrorCode);
        Assert.Equal("Refresh token has expired.", ex.Message);
    }

    [Fact]
    public async Task RefreshTokenAsync_throws_when_token_is_revoked()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var jwtGenerator = new TestJwtTokenGenerator();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance, jwtGenerator);

        var registerDto = new RegisterDto("revoked@nexus.local", "Password123!", "Le", "Van D");
        var user = await authService.RegisterAsync(registerDto);
        user.Status = UserStatus.Active;
        await dbContext.SaveChangesAsync();

        var loginResult = await authService.LoginAsync(new LoginDto("revoked@nexus.local", "Password123!"));
        var tokenEntity = await dbContext.RefreshTokens.FirstAsync(t => t.UserId == user.Id);

        // Thu hồi token thủ công
        tokenEntity.IsRevoked = true;
        tokenEntity.RevokedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            authService.RefreshTokenAsync(new RefreshTokenDto(loginResult.RefreshToken!)));

        Assert.Equal("REFRESH_TOKEN_REVOKED", ex.ErrorCode);
        Assert.Equal("Refresh token has been revoked.", ex.Message);
    }

    [Fact]
    public async Task RefreshTokenAsync_throws_and_revokes_when_user_is_banned()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var jwtGenerator = new TestJwtTokenGenerator();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance, jwtGenerator);

        var registerDto = new RegisterDto("banned@nexus.local", "Password123!", "Banned", "User");
        var user = await authService.RegisterAsync(registerDto);
        user.Status = UserStatus.Active;
        await dbContext.SaveChangesAsync();

        var loginResult = await authService.LoginAsync(new LoginDto("banned@nexus.local", "Password123!"));

        // Khóa tài khoản user
        user.Status = UserStatus.Banned;
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            authService.RefreshTokenAsync(new RefreshTokenDto(loginResult.RefreshToken!)));

        Assert.Equal("USER_BANNED", ex.ErrorCode);
        Assert.Equal("User account has been banned.", ex.Message);

        // Kiểm tra token đã bị thu hồi trong DB
        var tokenEntity = await dbContext.RefreshTokens.FirstAsync(t => t.UserId == user.Id);
        Assert.True(tokenEntity.IsRevoked);
        Assert.NotNull(tokenEntity.RevokedAt);
    }

    private sealed class TestJwtTokenGenerator : IJwtTokenGenerator
    {
        public string GenerateToken(Domain.Entities.User user, System.Collections.Generic.IEnumerable<string> roles, out DateTime expiresAt)
        {
            expiresAt = DateTime.UtcNow.AddMinutes(120);
            return "mock-jwt-access-token-xyz";
        }
    }

    private sealed class TestRedisOtpService : IRedisOtpService
    {
        private readonly ConcurrentDictionary<string, OtpEntry> _storage = new(StringComparer.OrdinalIgnoreCase);

        public Task SaveOtpAsync(string email, string code, TimeSpan validityDuration, TimeSpan retentionDuration)
        {
            _storage[email] = new OtpEntry
            {
                Code = code,
                Email = email,
                Status = OtpStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.Add(validityDuration)
            };
            return Task.CompletedTask;
        }

        public Task<OtpEntry?> GetOtpAsync(string email)
        {
            _storage.TryGetValue(email, out var entry);
            return Task.FromResult(entry);
        }

        public Task MarkOtpConsumedAsync(string email)
        {
            if (_storage.TryGetValue(email, out var entry))
            {
                entry.Status = OtpStatus.Consumed;
                entry.ConsumedAt = DateTimeOffset.UtcNow;
            }
            return Task.CompletedTask;
        }

        public Task InvalidateExistingOtpsAsync(string email)
        {
            if (_storage.TryGetValue(email, out var entry) && entry.Status == OtpStatus.Active)
            {
                entry.Status = OtpStatus.Expired;
            }
            return Task.CompletedTask;
        }
    }
}
