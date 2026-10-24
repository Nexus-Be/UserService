namespace User.Api.Services;

using System;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using ShoppingCart.Contracts.V1;
using User.Application.Interfaces;
using User.Application.Services.Auth;
using User.Domain.Exceptions;
using User.Infrastructure.Persistence;

public sealed class UsersGrpcService : UsersApi.UsersApiBase
{
    private readonly IAuthService _authService;
    private readonly AppDbContext _dbContext;

    public UsersGrpcService(IAuthService authService, AppDbContext dbContext)
    {
        _authService = authService;
        _dbContext = dbContext;
    }

    public override async Task<UserProfile> Register(RegisterRequest request, ServerCallContext context)
    {
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
                PhoneNumber: request.PhoneNumber
            );

            var user = await _authService.RegisterAsync(dto, context.CancellationToken);
            return MapToUserProfile(user);
        }
        catch (DomainException ex)
        {
            throw new RpcException(new Status(MapDomainErrorCodeToGrpcStatus(ex.ErrorCode), ex.Message));
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<UserProfile> VerifyOtp(VerifyOtpRequest request, ServerCallContext context)
    {
        try
        {
            var user = await _authService.VerifyOtpAsync(request.Email, request.Otp, context.CancellationToken);
            return MapToUserProfile(user);
        }
        catch (DomainException ex)
        {
            throw new RpcException(new Status(MapDomainErrorCodeToGrpcStatus(ex.ErrorCode), ex.Message));
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<OtpResponse> ResendOtp(ResendOtpRequest request, ServerCallContext context)
    {
        try
        {
            var success = await _authService.ResendOtpAsync(request.Email, context.CancellationToken);
            return new OtpResponse
            {
                Success = success,
                Message = "New OTP verification code has been dispatched to your email address.",
                Email = request.Email
            };
        }
        catch (DomainException ex)
        {
            throw new RpcException(new Status(MapDomainErrorCodeToGrpcStatus(ex.ErrorCode), ex.Message));
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    private static StatusCode MapDomainErrorCodeToGrpcStatus(string errorCode) => errorCode switch
    {
        "USER_NOT_FOUND" => StatusCode.NotFound,
        "EMAIL_ALREADY_EXISTS" => StatusCode.AlreadyExists,
        "OTP_ALREADY_USED" or "OTP_EXPIRED" or "USER_ALREADY_VERIFIED" => StatusCode.FailedPrecondition,
        _ => StatusCode.InvalidArgument
    };

    public override async Task<UserProfile> GetProfile(UserRequest request, ServerCallContext context)
    {
        var userId = context.RequestHeaders.Get("x-user-id")?.Value ?? request.UserId;
        if (!Guid.TryParse(userId, out var guid))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid user identifier."));
        }

        var user = await _dbContext.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .Include(u => u.SellerProfile)
            .FirstOrDefaultAsync(u => u.Id == guid, context.CancellationToken);

        if (user == null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "User account not found."));
        }

        return MapToUserProfile(user);
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
