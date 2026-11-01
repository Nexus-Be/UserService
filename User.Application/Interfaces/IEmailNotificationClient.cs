namespace User.Application.Interfaces;

using System.Threading;
using System.Threading.Tasks;

public interface IEmailNotificationClient
{
    Task<bool> SendOtpEmailAsync(
        string email,
        string fullName,
        string otpCode,
        int expiresInMinutes = 5,
        CancellationToken cancellationToken = default);

    Task<bool> SendSellerApprovedEmailAsync(
        string email,
        string fullName,
        string shopName,
        CancellationToken cancellationToken = default);

    Task<bool> SendSellerRejectedEmailAsync(
        string email,
        string fullName,
        string shopName,
        string reason,
        CancellationToken cancellationToken = default);
}
