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

    [Fact]
    public async Task LogoutAsync_succeeds_and_revokes_token()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var jwtGenerator = new TestJwtTokenGenerator();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance, jwtGenerator);

        var registerDto = new RegisterDto("logout_test@nexus.local", "Password123!", "Le", "Van E");
        var user = await authService.RegisterAsync(registerDto);
        user.Status = UserStatus.Active;
        await dbContext.SaveChangesAsync();

        var loginResult = await authService.LoginAsync(new LoginDto("logout_test@nexus.local", "Password123!"));
        Assert.NotNull(loginResult.RefreshToken);

        var result = await authService.LogoutAsync(loginResult.RefreshToken);
        Assert.True(result);

        var tokenEntity = await dbContext.RefreshTokens.FirstAsync(t => t.UserId == user.Id);
        Assert.True(tokenEntity.IsRevoked);
        Assert.NotNull(tokenEntity.RevokedAt);
    }

    [Fact]
    public async Task LogoutAsync_is_idempotent_when_token_already_revoked()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var jwtGenerator = new TestJwtTokenGenerator();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance, jwtGenerator);

        var registerDto = new RegisterDto("idempotent@nexus.local", "Password123!", "Pham", "Van F");
        var user = await authService.RegisterAsync(registerDto);
        user.Status = UserStatus.Active;
        await dbContext.SaveChangesAsync();

        var loginResult = await authService.LoginAsync(new LoginDto("idempotent@nexus.local", "Password123!"));
        
        // Đăng xuất lần 1
        var firstResult = await authService.LogoutAsync(loginResult.RefreshToken!);
        Assert.True(firstResult);

        // Đăng xuất lần 2 (idempotent)
        var secondResult = await authService.LogoutAsync(loginResult.RefreshToken!);
        Assert.True(secondResult);
    }

    [Fact]
    public async Task LogoutAsync_throws_when_token_is_invalid()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            authService.LogoutAsync("non-existent-refresh-token-xyz"));

        Assert.Equal("INVALID_REFRESH_TOKEN", ex.ErrorCode);
        Assert.Equal("Invalid refresh token.", ex.Message);
    }

    [Fact]
    public async Task LogoutAsync_throws_when_token_is_empty()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance);

        var ex = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            authService.LogoutAsync("   "));

        Assert.Equal("VAL_INVALID_INPUT", ex.ErrorCode);
        Assert.Equal("Refresh token is required.", ex.Message);
    }

    [Fact]
    public async Task LogoutAllAsync_revokes_all_active_tokens_for_user()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance);

        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();

        // 2 token active + 1 token đã revoke cho user 1
        dbContext.RefreshTokens.AddRange(
            new Domain.Entities.RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = userId1,
                TokenHash = "hash1",
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
                IsRevoked = false
            },
            new Domain.Entities.RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = userId1,
                TokenHash = "hash2",
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
                IsRevoked = false
            },
            new Domain.Entities.RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = userId1,
                TokenHash = "hash3",
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
                IsRevoked = true,
                RevokedAt = DateTimeOffset.UtcNow.AddHours(-1)
            },
            // Token active cho user 2 (không bị ảnh hưởng)
            new Domain.Entities.RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = userId2,
                TokenHash = "hash4",
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
                IsRevoked = false
            }
        );
        await dbContext.SaveChangesAsync();

        var countRevoked = await authService.LogoutAllAsync(userId1);
        Assert.Equal(2, countRevoked);

        // Kiểm tra tất cả token của user 1 đều đã bị thu hồi
        var user1Tokens = await dbContext.RefreshTokens.Where(t => t.UserId == userId1).ToListAsync();
        Assert.All(user1Tokens, t => Assert.True(t.IsRevoked));

        // Token của user 2 vẫn active
        var user2Token = await dbContext.RefreshTokens.FirstAsync(t => t.UserId == userId2);
        Assert.False(user2Token.IsRevoked);
    }

    [Fact]
    public async Task LogoutAllAsync_throws_when_user_id_is_empty()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance);

        var ex = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            authService.LogoutAllAsync(Guid.Empty));

        Assert.Equal("VAL_INVALID_INPUT", ex.ErrorCode);
        Assert.Equal("User identifier is required.", ex.Message);
    }

    [Fact]
    public async Task ForgotPasswordAsync_when_user_exists_generates_otp_and_returns_success()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance);

        var email = "forgot_user@nexus.local";
        var user = new Domain.Entities.User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = "hashed_pw",
            FirstName = "Forgot",
            LastName = "User",
            Status = Domain.Enums.UserStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            IsDeleted = false
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var result = await authService.ForgotPasswordAsync(new ForgotPasswordDto(email));

        Assert.NotNull(result);
        Assert.Equal(email, result.Email);
        Assert.Equal(600, result.OtpExpiresIn);
        Assert.Equal("If the email exists in our system, password reset instructions have been sent.", result.Message);

        var storedOtp = await otpService.GetOtpAsync(email, purpose: "password_reset");
        Assert.NotNull(storedOtp);
        Assert.Equal(6, storedOtp.Code.Length);
        Assert.Equal(OtpStatus.Active, storedOtp.Status);
    }

    [Fact]
    public async Task ForgotPasswordAsync_when_user_not_found_returns_generic_success_without_throwing()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance);

        var nonExistentEmail = "ghost@nexus.local";
        var result = await authService.ForgotPasswordAsync(new ForgotPasswordDto(nonExistentEmail));

        Assert.NotNull(result);
        Assert.Equal(nonExistentEmail, result.Email);
        Assert.Equal("If the email exists in our system, password reset instructions have been sent.", result.Message);

        // Đảm bảo không sinh OTP trong Redis cho email không tồn tại
        var storedOtp = await otpService.GetOtpAsync(nonExistentEmail, purpose: "password_reset");
        Assert.Null(storedOtp);
    }

    [Fact]
    public async Task ForgotPasswordAsync_when_user_banned_returns_generic_success_without_otp()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance);

        var bannedEmail = "banned@nexus.local";
        var user = new Domain.Entities.User
        {
            Id = Guid.NewGuid(),
            Email = bannedEmail,
            PasswordHash = "hashed_pw",
            FirstName = "Banned",
            LastName = "User",
            Status = Domain.Enums.UserStatus.Banned,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            IsDeleted = false
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var result = await authService.ForgotPasswordAsync(new ForgotPasswordDto(bannedEmail));

        Assert.NotNull(result);
        Assert.Equal(bannedEmail, result.Email);
        Assert.Equal("If the email exists in our system, password reset instructions have been sent.", result.Message);

        var storedOtp = await otpService.GetOtpAsync(bannedEmail, purpose: "password_reset");
        Assert.Null(storedOtp);
    }

    [Fact]
    public async Task ForgotPasswordAsync_when_email_empty_throws_business_rule_exception()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance);

        var ex = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            authService.ForgotPasswordAsync(new ForgotPasswordDto("   ")));

        Assert.Equal("VAL_INVALID_EMAIL", ex.ErrorCode);
        Assert.Equal("Email address is required.", ex.Message);
    }

    [Fact]
    public async Task ForgotPasswordAsync_when_email_format_invalid_throws_business_rule_exception()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance);

        var ex = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            authService.ForgotPasswordAsync(new ForgotPasswordDto("invalid-email-format")));

        Assert.Equal("VAL_INVALID_EMAIL", ex.ErrorCode);
        Assert.Equal("Email address format is invalid.", ex.Message);
    }

    [Fact]
    public async Task ResetPasswordAsync_succeeds_when_valid_otp_and_passwords()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance);

        var email = "reset_user@nexus.local";
        var userId = Guid.NewGuid();
        var oldHash = BCrypt.Net.BCrypt.HashPassword("OldPassword123");

        var user = new Domain.Entities.User
        {
            Id = userId,
            Email = email,
            PasswordHash = oldHash,
            FirstName = "Reset",
            LastName = "User",
            Status = Domain.Enums.UserStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            IsDeleted = false
        };
        dbContext.Users.Add(user);

        // Tạo 2 refresh token đang hoạt động
        dbContext.RefreshTokens.Add(new Domain.Entities.RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = "token_hash_1",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            IsRevoked = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        dbContext.RefreshTokens.Add(new Domain.Entities.RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = "token_hash_2",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            IsRevoked = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync();

        // Lưu OTP vào Redis
        var otpCode = "654321";
        await otpService.SaveOtpAsync(email, otpCode, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(15), purpose: "password_reset");

        var dto = new ResetPasswordDto(email, otpCode, "NewPassword123!", "NewPassword123!");
        var result = await authService.ResetPasswordAsync(dto);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("Password has been reset successfully.", result.Message);

        // Kiểm tra mật khẩu trong DB đã đổi và khớp với hash BCrypt mới
        var dbUser = await dbContext.Users.FindAsync(userId);
        Assert.NotNull(dbUser);
        Assert.NotEqual(oldHash, dbUser.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify("NewPassword123!", dbUser.PasswordHash));

        // Kiểm tra OTP đã chuyển thành Consumed
        var storedOtp = await otpService.GetOtpAsync(email, purpose: "password_reset");
        Assert.NotNull(storedOtp);
        Assert.Equal(OtpStatus.Consumed, storedOtp.Status);

        // Kiểm tra toàn bộ refresh token cũ đều bị thu hồi
        var tokens = await dbContext.RefreshTokens.Where(t => t.UserId == userId).ToListAsync();
        Assert.Equal(2, tokens.Count);
        Assert.All(tokens, t => Assert.True(t.IsRevoked));
    }

    [Fact]
    public async Task ResetPasswordAsync_throws_when_otp_expired_or_not_found()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance);

        var email = "no_otp@nexus.local";
        var user = new Domain.Entities.User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = "hashed",
            FirstName = "Test",
            LastName = "User",
            Status = Domain.Enums.UserStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var dto = new ResetPasswordDto(email, "123456", "NewPassword123!", "NewPassword123!");
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            authService.ResetPasswordAsync(dto));

        Assert.Equal("OTP_EXPIRED", ex.ErrorCode);
    }

    [Fact]
    public async Task ResetPasswordAsync_throws_when_otp_code_invalid()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance);

        var email = "wrong_otp@nexus.local";
        var user = new Domain.Entities.User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = "hashed",
            FirstName = "Test",
            LastName = "User",
            Status = Domain.Enums.UserStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        await otpService.SaveOtpAsync(email, "111222", TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(15), purpose: "password_reset");

        var dto = new ResetPasswordDto(email, "999888", "NewPassword123!", "NewPassword123!");
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            authService.ResetPasswordAsync(dto));

        Assert.Equal("OTP_INVALID", ex.ErrorCode);
    }

    [Fact]
    public async Task ResetPasswordAsync_throws_when_otp_already_consumed()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance);

        var email = "consumed_otp@nexus.local";
        var user = new Domain.Entities.User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = "hashed",
            FirstName = "Test",
            LastName = "User",
            Status = Domain.Enums.UserStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        await otpService.SaveOtpAsync(email, "123456", TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(15), purpose: "password_reset");
        await otpService.MarkOtpConsumedAsync(email, purpose: "password_reset");

        var dto = new ResetPasswordDto(email, "123456", "NewPassword123!", "NewPassword123!");
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            authService.ResetPasswordAsync(dto));

        Assert.Equal("OTP_ALREADY_USED", ex.ErrorCode);
    }

    [Fact]
    public async Task ResetPasswordAsync_throws_when_password_mismatch()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance);

        var dto = new ResetPasswordDto("mismatch@nexus.local", "123456", "PasswordA123", "PasswordB123");
        var ex = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            authService.ResetPasswordAsync(dto));

        Assert.Equal("VAL_PASSWORD_MISMATCH", ex.ErrorCode);
    }

    [Fact]
    public async Task ResetPasswordAsync_throws_when_password_too_short()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance);

        var dto = new ResetPasswordDto("short@nexus.local", "123456", "short", "short");
        var ex = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            authService.ResetPasswordAsync(dto));

        Assert.Equal("VAL_INVALID_PASSWORD", ex.ErrorCode);
    }

    [Fact]
    public async Task ResetPasswordAsync_throws_when_user_not_found()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance);

        var dto = new ResetPasswordDto("nonexistent@nexus.local", "123456", "ValidPass123", "ValidPass123");
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            authService.ResetPasswordAsync(dto));

        Assert.Equal("USER_NOT_FOUND", ex.ErrorCode);
    }

    [Fact]
    public async Task ResetPasswordAsync_throws_when_otp_format_invalid()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance);

        var dto = new ResetPasswordDto("test@nexus.local", "12ab", "ValidPass123", "ValidPass123");
        var ex = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            authService.ResetPasswordAsync(dto));

        Assert.Equal("VAL_INVALID_OTP", ex.ErrorCode);
    }

    [Fact]
    public async Task ChangePasswordAsync_succeeds_when_valid_credentials()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance);

        var userId = Guid.NewGuid();
        var user = new Domain.Entities.User
        {
            Id = userId,
            Email = "change_pass@nexus.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("OldPassword123!"),
            FirstName = "Test",
            LastName = "User",
            Status = Domain.Enums.UserStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Users.Add(user);

        // Thêm 2 refresh token đang hoạt động
        var token1 = new Domain.Entities.RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = "hash1",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            IsRevoked = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var token2 = new Domain.Entities.RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = "hash2",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            IsRevoked = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.RefreshTokens.AddRange(token1, token2);
        await dbContext.SaveChangesAsync();

        var dto = new ChangePasswordDto("OldPassword123!", "NewSecretPass456!", "NewSecretPass456!");
        var result = await authService.ChangePasswordAsync(userId, dto);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("Password changed successfully.", result.Message);

        // Kiểm tra mật khẩu mới đã được băm bằng BCrypt
        var updatedUser = await dbContext.Users.FindAsync(userId);
        Assert.NotNull(updatedUser);
        Assert.True(BCrypt.Net.BCrypt.Verify("NewSecretPass456!", updatedUser.PasswordHash));
        Assert.False(BCrypt.Net.BCrypt.Verify("OldPassword123!", updatedUser.PasswordHash));

        // Kiểm tra tất cả refresh token cũ đã bị thu hồi
        var activeTokens = await dbContext.RefreshTokens.Where(t => t.UserId == userId && !t.IsRevoked).ToListAsync();
        Assert.Empty(activeTokens);
    }

    [Fact]
    public async Task ChangePasswordAsync_throws_when_old_password_incorrect()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance);

        var userId = Guid.NewGuid();
        var user = new Domain.Entities.User
        {
            Id = userId,
            Email = "wrong_old_pass@nexus.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("ActualOldPassword123!"),
            FirstName = "Test",
            LastName = "User",
            Status = Domain.Enums.UserStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var dto = new ChangePasswordDto("IncorrectOldPassword!", "NewSecretPass456!", "NewSecretPass456!");
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            authService.ChangePasswordAsync(userId, dto));

        Assert.Equal("INVALID_CREDENTIALS", ex.ErrorCode);
    }

    [Fact]
    public async Task ChangePasswordAsync_throws_when_new_password_same_as_old()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance);

        var userId = Guid.NewGuid();
        var user = new Domain.Entities.User
        {
            Id = userId,
            Email = "same_pass@nexus.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("SamePassword123!"),
            FirstName = "Test",
            LastName = "User",
            Status = Domain.Enums.UserStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var dto = new ChangePasswordDto("SamePassword123!", "SamePassword123!", "SamePassword123!");
        var ex = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            authService.ChangePasswordAsync(userId, dto));

        Assert.Equal("VAL_SAME_PASSWORD", ex.ErrorCode);
    }

    [Fact]
    public async Task ChangePasswordAsync_throws_when_confirm_password_mismatch()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance);

        var userId = Guid.NewGuid();
        var dto = new ChangePasswordDto("OldPassword123!", "NewSecretPass456!", "DifferentPass789!");
        var ex = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            authService.ChangePasswordAsync(userId, dto));

        Assert.Equal("VAL_PASSWORD_MISMATCH", ex.ErrorCode);
    }

    [Fact]
    public async Task ChangePasswordAsync_throws_when_new_password_too_short()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance);

        var userId = Guid.NewGuid();
        var dto = new ChangePasswordDto("OldPassword123!", "short", "short");
        var ex = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            authService.ChangePasswordAsync(userId, dto));

        Assert.Equal("VAL_INVALID_PASSWORD", ex.ErrorCode);
    }

    [Fact]
    public async Task ChangePasswordAsync_throws_when_old_password_empty()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance);

        var userId = Guid.NewGuid();
        var dto = new ChangePasswordDto("", "NewSecretPass456!", "NewSecretPass456!");
        var ex = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            authService.ChangePasswordAsync(userId, dto));

        Assert.Equal("VAL_INVALID_PASSWORD", ex.ErrorCode);
    }

    [Fact]
    public async Task ChangePasswordAsync_throws_when_user_id_is_empty()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance);

        var dto = new ChangePasswordDto("OldPassword123!", "NewSecretPass456!", "NewSecretPass456!");
        var ex = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            authService.ChangePasswordAsync(Guid.Empty, dto));

        Assert.Equal("VAL_INVALID_INPUT", ex.ErrorCode);
    }

    [Fact]
    public async Task ChangePasswordAsync_throws_when_user_not_found()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance);

        var nonExistentUserId = Guid.NewGuid();
        var dto = new ChangePasswordDto("OldPassword123!", "NewSecretPass456!", "NewSecretPass456!");
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            authService.ChangePasswordAsync(nonExistentUserId, dto));

        Assert.Equal("USER_NOT_FOUND", ex.ErrorCode);
    }

    [Fact]
    public async Task ChangePasswordAsync_throws_when_user_is_banned()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance);

        var userId = Guid.NewGuid();
        var user = new Domain.Entities.User
        {
            Id = userId,
            Email = "banned_user@nexus.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("OldPassword123!"),
            FirstName = "Test",
            LastName = "User",
            Status = Domain.Enums.UserStatus.Banned,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var dto = new ChangePasswordDto("OldPassword123!", "NewSecretPass456!", "NewSecretPass456!");
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            authService.ChangePasswordAsync(userId, dto));

        Assert.Equal("USER_BANNED", ex.ErrorCode);
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

        private static string Key(string email, string purpose) => $"{purpose}:{email}";

        public Task SaveOtpAsync(string email, string code, TimeSpan validityDuration, TimeSpan retentionDuration, string purpose = "registration")
        {
            _storage[Key(email, purpose)] = new OtpEntry
            {
                Code = code,
                Email = email,
                Status = OtpStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.Add(validityDuration)
            };
            return Task.CompletedTask;
        }

        public Task<OtpEntry?> GetOtpAsync(string email, string purpose = "registration")
        {
            _storage.TryGetValue(Key(email, purpose), out var entry);
            return Task.FromResult(entry);
        }

        public Task MarkOtpConsumedAsync(string email, string purpose = "registration")
        {
            if (_storage.TryGetValue(Key(email, purpose), out var entry))
            {
                entry.Status = OtpStatus.Consumed;
                entry.ConsumedAt = DateTimeOffset.UtcNow;
            }
            return Task.CompletedTask;
        }

        public Task InvalidateExistingOtpsAsync(string email, string purpose = "registration")
        {
            if (_storage.TryGetValue(Key(email, purpose), out var entry) && entry.Status == OtpStatus.Active)
            {
                entry.Status = OtpStatus.Expired;
            }
            return Task.CompletedTask;
        }
    }
}
