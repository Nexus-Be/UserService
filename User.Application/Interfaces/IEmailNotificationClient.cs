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
}
