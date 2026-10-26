namespace User.Api.Services;

using System;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using ShoppingCart.Contracts.V1;
using User.Application.Interfaces;
using User.Application.Services.Auth;
using User.Application.Services.Profile;
using User.Domain.Exceptions;

public sealed class UsersGrpcService : UsersApi.UsersApiBase
{
    private readonly IAuthService _authService;
    private readonly IProfileService _profileService;
    private readonly ILogger<UsersGrpcService>? _logger;

    public UsersGrpcService(
        IAuthService authService,
        IProfileService profileService,
        ILogger<UsersGrpcService>? logger = null)
    {
        _authService = authService;
        _profileService = profileService;
        _logger = logger;
    }

    public override async Task<UserProfile> Register(RegisterRequest request, ServerCallContext context)
    {
        _logger?.LogInformation("[gRPC Register] Incoming request for Email: '{Email}', Username: '{Username}', DisplayName: '{DisplayName}'", request.Email, request.Username, request.DisplayName);
        try
        {
            var firstName = request.FirstName;
            var lastName = request.LastName;

            if (string.IsNullOrWhiteSpace(firstName) && !string.IsNullOrWhiteSpace(request.DisplayName))
            {
                var parts = request.DisplayName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                firstName = parts[0];
                lastName = parts.Length > 1 ? parts[1] : (string.IsNullOrWhiteSpace(lastName) ? "User" : lastName);
            }
            else if (string.IsNullOrWhiteSpace(firstName) && !string.IsNullOrWhiteSpace(request.Username))
            {
                firstName = request.Username.Trim();
                if (string.IsNullOrWhiteSpace(lastName))
                {
                    lastName = "User";
                }
            }
            else if (string.IsNullOrWhiteSpace(lastName))
            {
                lastName = "User";
            }

            var dto = new RegisterDto(
                Email: request.Email,
                Password: request.Password,
                FirstName: firstName ?? "User",
                LastName: lastName ?? "User",
                PhoneNumber: request.PhoneNumber,
                Username: string.IsNullOrWhiteSpace(request.Username) ? null : request.Username.Trim()
            );

            var user = await _authService.RegisterAsync(dto, context.CancellationToken);
            _logger?.LogInformation("[gRPC Register] Succeeded for '{Email}' -> UserId: {UserId}", user.Email, user.Id);
            return MapToUserProfile(user);
        }
        catch (DomainException ex)
        {
            var grpcStatus = MapDomainErrorCodeToGrpcStatus(ex.ErrorCode);
            _logger?.LogWarning("[gRPC Register] DomainException: [{ErrorCode}] {Message} -> gRPC {Status}", ex.ErrorCode, ex.Message, grpcStatus);
            throw new RpcException(new Status(grpcStatus, ex.Message));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[gRPC Register] Unhandled Exception: {Message}", ex.Message);
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<UserProfile> VerifyOtp(VerifyOtpRequest request, ServerCallContext context)
    {
        _logger?.LogInformation("[gRPC VerifyOtp] Incoming request for Email: '{Email}', OTP: '{Otp}'", request.Email, request.Otp);
        try
        {
            var user = await _authService.VerifyOtpAsync(request.Email, request.Otp, context.CancellationToken);
            _logger?.LogInformation("[gRPC VerifyOtp] Succeeded for '{Email}' -> Status: {Status}", user.Email, user.Status);
            return MapToUserProfile(user);
        }
        catch (DomainException ex)
        {
            var grpcStatus = MapDomainErrorCodeToGrpcStatus(ex.ErrorCode);
            _logger?.LogWarning("[gRPC VerifyOtp] DomainException: [{ErrorCode}] {Message} -> gRPC {Status}", ex.ErrorCode, ex.Message, grpcStatus);
            throw new RpcException(new Status(grpcStatus, ex.Message));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[gRPC VerifyOtp] Unhandled Exception: {Message}", ex.Message);
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<OtpResponse> ResendOtp(ResendOtpRequest request, ServerCallContext context)
    {
        _logger?.LogInformation("[gRPC ResendOtp] Incoming request for Email: '{Email}'", request.Email);
        try
        {
            var success = await _authService.ResendOtpAsync(request.Email, context.CancellationToken);
            _logger?.LogInformation("[gRPC ResendOtp] Succeeded for '{Email}'", request.Email);
            return new OtpResponse
            {
                Success = success,
                Message = "New OTP verification code has been dispatched to your email address.",
                Email = request.Email
            };
        }
        catch (DomainException ex)
        {
            var grpcStatus = MapDomainErrorCodeToGrpcStatus(ex.ErrorCode);
            _logger?.LogWarning("[gRPC ResendOtp] DomainException: [{ErrorCode}] {Message} -> gRPC {Status}", ex.ErrorCode, ex.Message, grpcStatus);
            throw new RpcException(new Status(grpcStatus, ex.Message));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[gRPC ResendOtp] Unhandled Exception: {Message}", ex.Message);
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<LoginResponse> Login(LoginRequest request, ServerCallContext context)
    {
        _logger?.LogInformation("[gRPC Login] Incoming request for Username/Email: '{Username}'", request.Username);
        try
        {
            var dto = new LoginDto(
                UsernameOrEmail: request.Username,
                Password: request.Password
            );

            var result = await _authService.LoginAsync(dto, context.CancellationToken);

            var response = new LoginResponse
            {
                AccessToken = result.AccessToken,
                ExpiresAt = result.ExpiresAt,
                UserId = result.UserId,
                RefreshToken = result.RefreshToken ?? string.Empty
            };

            foreach (var role in result.Roles)
            {
                response.Roles.Add(role);
            }

            _logger?.LogInformation("[gRPC Login] Succeeded for '{Username}' -> UserId: {UserId}, Roles: [{Roles}]", request.Username, result.UserId, string.Join(", ", result.Roles));
            return response;
        }
        catch (DomainException ex)
        {
            var grpcStatus = MapDomainErrorCodeToGrpcStatus(ex.ErrorCode);
            _logger?.LogWarning("[gRPC Login] DomainException: [{ErrorCode}] {Message} -> gRPC {Status}", ex.ErrorCode, ex.Message, grpcStatus);
            throw new RpcException(new Status(grpcStatus, ex.Message));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[gRPC Login] Unhandled Exception: {Message}", ex.Message);
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<LogoutResponse> Logout(LogoutRequest request, ServerCallContext context)
    {
        _logger?.LogInformation("[gRPC Logout] Incoming logout request");
        try
        {
            var success = await _authService.LogoutAsync(request.RefreshToken, context.CancellationToken);
            _logger?.LogInformation("[gRPC Logout] Succeeded");
            return new LogoutResponse
            {
                Success = success,
                Message = "Logged out successfully."
            };
        }
        catch (DomainException ex)
        {
            var grpcStatus = MapDomainErrorCodeToGrpcStatus(ex.ErrorCode);
            _logger?.LogWarning("[gRPC Logout] DomainException: [{ErrorCode}] {Message} -> gRPC {Status}", ex.ErrorCode, ex.Message, grpcStatus);
            throw new RpcException(new Status(grpcStatus, ex.Message));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[gRPC Logout] Unhandled Exception: {Message}", ex.Message);
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<LogoutAllResponse> LogoutAll(LogoutAllRequest request, ServerCallContext context)
    {
        var userIdStr = request.Caller?.UserId ?? context.RequestHeaders.Get("x-user-id")?.Value;
        _logger?.LogInformation("[gRPC LogoutAll] Incoming request for UserId: '{UserId}'", userIdStr);

        if (!Guid.TryParse(userIdStr, out var userId))
        {
            _logger?.LogWarning("[gRPC LogoutAll] Invalid or missing user identifier: '{UserId}'", userIdStr);
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid user identifier."));
        }

        try
        {
            var countRevoked = await _authService.LogoutAllAsync(userId, context.CancellationToken);
            _logger?.LogInformation("[gRPC LogoutAll] Succeeded for UserId: {UserId} -> Revoked: {Count}", userId, countRevoked);
            return new LogoutAllResponse
            {
                CountRevoked = countRevoked,
                Message = "All sessions have been revoked."
            };
        }
        catch (DomainException ex)
        {
            var grpcStatus = MapDomainErrorCodeToGrpcStatus(ex.ErrorCode);
            _logger?.LogWarning("[gRPC LogoutAll] DomainException: [{ErrorCode}] {Message} -> gRPC {Status}", ex.ErrorCode, ex.Message, grpcStatus);
            throw new RpcException(new Status(grpcStatus, ex.Message));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[gRPC LogoutAll] Unhandled Exception: {Message}", ex.Message);
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    private static StatusCode MapDomainErrorCodeToGrpcStatus(string errorCode) => errorCode switch
    {
        "USER_NOT_FOUND" => StatusCode.NotFound,
        "EMAIL_ALREADY_EXISTS" or "USERNAME_ALREADY_EXISTS" or "PHONE_ALREADY_EXISTS" => StatusCode.AlreadyExists,
        "INVALID_CREDENTIALS" or "INVALID_REFRESH_TOKEN" => StatusCode.Unauthenticated,
        "USER_NOT_VERIFIED" => StatusCode.FailedPrecondition,
        "USER_BANNED" or "USER_SUSPENDED" => StatusCode.PermissionDenied,
        "OTP_ALREADY_USED" or "OTP_EXPIRED" or "USER_ALREADY_VERIFIED" or "REFRESH_TOKEN_EXPIRED" or "REFRESH_TOKEN_REVOKED" => StatusCode.FailedPrecondition,
        _ => StatusCode.InvalidArgument
    };

    public override async Task<UserProfile> GetProfile(UserRequest request, ServerCallContext context)
    {
        var userId = context.RequestHeaders.Get("x-user-id")?.Value ?? request.UserId;
        _logger?.LogInformation("[gRPC GetProfile] Incoming request for UserId: '{UserId}'", userId);
        if (!Guid.TryParse(userId, out var guid))
        {
            _logger?.LogWarning("[gRPC GetProfile] Invalid user identifier: '{UserId}'", userId);
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid user identifier."));
        }

        try
        {
            var dto = await _profileService.GetProfileAsync(guid, context.CancellationToken);
            _logger?.LogInformation("[gRPC GetProfile] Succeeded for '{Email}' (UserId: {UserId})", dto.Email, dto.Id);

            var profile = new UserProfile
            {
                Id = dto.Id,
                DisplayName = dto.DisplayName,
                Email = dto.Email,
                Status = dto.Status,
                ShopName = dto.ShopName ?? string.Empty
            };

            foreach (var role in dto.Roles)
            {
                profile.Roles.Add(role);
            }

            return profile;
        }
        catch (DomainException ex)
        {
            var grpcStatus = MapDomainErrorCodeToGrpcStatus(ex.ErrorCode);
            _logger?.LogWarning("[gRPC GetProfile] DomainException: [{ErrorCode}] {Message} -> gRPC {Status}", ex.ErrorCode, ex.Message, grpcStatus);
            throw new RpcException(new Status(grpcStatus, ex.Message));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[gRPC GetProfile] Unhandled Exception: {Message}", ex.Message);
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<UserProfile> UpdateProfile(UpdateProfileRequest request, ServerCallContext context)
    {
        var userId = context.RequestHeaders.Get("x-user-id")?.Value ?? request.UserId;
        _logger?.LogInformation("[gRPC UpdateProfile] Incoming request for UserId: '{UserId}'", userId);
        if (!Guid.TryParse(userId, out var guid))
        {
            _logger?.LogWarning("[gRPC UpdateProfile] Invalid user identifier: '{UserId}'", userId);
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid user identifier."));
        }

        try
        {
            string? firstName = string.IsNullOrWhiteSpace(request.FirstName) ? null : request.FirstName;
            string? lastName = string.IsNullOrWhiteSpace(request.LastName) ? null : request.LastName;

            if (firstName == null && lastName == null && !string.IsNullOrWhiteSpace(request.DisplayName))
            {
                var parts = request.DisplayName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0) firstName = parts[0];
                if (parts.Length > 1) lastName = parts[1];
            }

            var updateDto = new UpdateProfileDto(
                FirstName: firstName,
                LastName: lastName,
                PhoneNumber: string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber,
                Address: string.IsNullOrWhiteSpace(request.Address) ? null : request.Address,
                AvatarUrl: string.IsNullOrWhiteSpace(request.AvatarUrl) ? null : request.AvatarUrl
            );

            var dto = await _profileService.UpdateProfileAsync(guid, updateDto, context.CancellationToken);
            _logger?.LogInformation("[gRPC UpdateProfile] Succeeded for '{Email}' (UserId: {UserId})", dto.Email, dto.Id);

            var profile = new UserProfile
            {
                Id = dto.Id,
                DisplayName = dto.DisplayName,
                Email = dto.Email,
                Status = dto.Status,
                ShopName = dto.ShopName ?? string.Empty
            };

            foreach (var role in dto.Roles)
            {
                profile.Roles.Add(role);
            }

            return profile;
        }
        catch (DomainException ex)
        {
            var grpcStatus = MapDomainErrorCodeToGrpcStatus(ex.ErrorCode);
            _logger?.LogWarning("[gRPC UpdateProfile] DomainException: [{ErrorCode}] {Message} -> gRPC {Status}", ex.ErrorCode, ex.Message, grpcStatus);
            throw new RpcException(new Status(grpcStatus, ex.Message));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[gRPC UpdateProfile] Unhandled Exception: {Message}", ex.Message);
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    private static UserProfile MapToUserProfile(Domain.Entities.User user)
    {
        var profile = new UserProfile
        {
            Id = user.Id.ToString(),
            DisplayName = $"{user.FirstName} {user.LastName}".Trim(),
            Email = user.Email,
            Status = user.Status.ToString(),
            ShopName = user.SellerProfile?.ShopName ?? string.Empty
        };

        if (user.UserRoles != null && user.UserRoles.Count > 0)
        {
            foreach (var ur in user.UserRoles)
            {
                var roleName = ur.Role?.RoleName?.ToLowerInvariant() ?? "buyer";
                profile.Roles.Add(roleName);
            }
        }
        else
        {
            profile.Roles.Add("buyer");
        }

        return profile;
    }
}
