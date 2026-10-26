namespace User.Application.Services.Auth;

using System.Threading;
using System.Threading.Tasks;
using User.Domain.Entities;

public interface IAuthService
{
    Task<User> RegisterAsync(RegisterDto dto, CancellationToken cancellationToken = default);
    Task<User> VerifyOtpAsync(string email, string otp, CancellationToken cancellationToken = default);
    Task<bool> ResendOtpAsync(string email, CancellationToken cancellationToken = default);
    Task<LoginResultDto> LoginAsync(LoginDto dto, CancellationToken cancellationToken = default);
    Task<RefreshTokenResultDto> RefreshTokenAsync(RefreshTokenDto dto, CancellationToken cancellationToken = default);
    Task<bool> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<int> LogoutAllAsync(System.Guid userId, CancellationToken cancellationToken = default);
}
