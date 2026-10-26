namespace User.Application.Interfaces;

using System;
using System.Threading.Tasks;
using User.Application.Models;

public interface IRedisOtpService
{
    Task SaveOtpAsync(string email, string code, TimeSpan validityDuration, TimeSpan retentionDuration, string purpose = "registration");
    Task<OtpEntry?> GetOtpAsync(string email, string purpose = "registration");
    Task MarkOtpConsumedAsync(string email, string purpose = "registration");
    Task InvalidateExistingOtpsAsync(string email, string purpose = "registration");
}
