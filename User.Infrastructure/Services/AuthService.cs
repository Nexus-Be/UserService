namespace User.Infrastructure.Services;

using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexus.Contracts.Events;
using User.Application.DTOs;
using User.Application.Interfaces;
using User.Application.Models;
using User.Domain.Entities;
using User.Domain.Enums;
using User.Domain.Exceptions;
using User.Infrastructure.Persistence;

public sealed class AuthService : IAuthService
{
    private readonly AppDbContext _dbContext;
    private readonly IRedisOtpService _otpService;
    private readonly IPublishEndpoint? _publishEndpoint;
    private readonly ILogger<AuthService> _logger;

    private static readonly Guid BuyerRoleId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public AuthService(
        AppDbContext dbContext,
        IRedisOtpService otpService,
        ILogger<AuthService> logger,
        IPublishEndpoint? publishEndpoint = null)
    {
        _dbContext = dbContext;
        _otpService = otpService;
        _logger = logger;
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
            // Cập nhật lại mật khẩu và thông tin mới, vô hiệu hóa các mã OTP cũ
            existingUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
            existingUser.FirstName = dto.FirstName.Trim();
            existingUser.LastName = dto.LastName.Trim();
            if (!string.IsNullOrWhiteSpace(dto.PhoneNumber))
            {
                existingUser.PhoneNumber = dto.PhoneNumber.Trim();
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

            // Gán role mặc định: Buyer (ưu tiên tìm ID từ DB nếu có, fallback về BuyerRoleId)
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

        // Lưu vào Redis: Hiệu lực 5 phút, lưu trữ tối đa 15 phút
        await _otpService.SaveOtpAsync(email, otpCode, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15));

        // Lưu CSDL
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Bắn sự kiện lên RabbitMQ cho Worker gửi mail bất đồng bộ (không làm treo luồng chính)
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
            throw new DomainException("OTP_EXPIRED", "OTP code has expired or does not exist.");
        }

        if (otpEntry.Status == OtpStatus.Consumed)
        {
            throw new DomainException("OTP_ALREADY_USED", "This OTP code has already been used.");
        }

        if (otpEntry.Status == OtpStatus.Expired || DateTimeOffset.UtcNow > otpEntry.ExpiresAt)
        {
            throw new DomainException("OTP_EXPIRED", "OTP code has expired. Please request a new code.");
        }

        if (!string.Equals(otpEntry.Code, otp.Trim(), StringComparison.Ordinal))
        {
            throw new DomainException("OTP_INVALID", "OTP code is invalid.");
        }

        // Xác nhận thành công: Chuyển trạng thái OTP sang Consumed (không xóa ngay để chống Replay)
        await _otpService.MarkOtpConsumedAsync(normalizedEmail);

        // Kích hoạt tài khoản người dùng
        user.Status = UserStatus.Active;
        user.EmailVerifiedAt = DateTimeOffset.UtcNow;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("User '{Email}' successfully verified account and status changed to Active.", normalizedEmail);

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
            throw new DomainException("USER_NOT_FOUND", "User account not found.");
        }

        if (user.Status == UserStatus.Active)
        {
            throw new DomainException("USER_ALREADY_VERIFIED", "User account has already been verified.");
        }

        // Vô hiệu hóa các mã OTP cũ trước đó
        await _otpService.InvalidateExistingOtpsAsync(normalizedEmail);

        // Sinh mã OTP mới
        var newOtpCode = Random.Shared.Next(100000, 999999).ToString();
        await _otpService.SaveOtpAsync(normalizedEmail, newOtpCode, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15));

        // Bắn sự kiện gửi mail
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
}
