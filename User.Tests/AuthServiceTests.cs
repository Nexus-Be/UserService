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
