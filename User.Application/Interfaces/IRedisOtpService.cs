namespace User.Application.Interfaces;

using System;
using System.Threading.Tasks;
using User.Application.Models;

public interface IRedisOtpService
{
    Task SaveOtpAsync(string email, string code, TimeSpan validityDuration, TimeSpan retentionDuration);
    Task<OtpEntry?> GetOtpAsync(string email);
    Task MarkOtpConsumedAsync(string email);
    Task InvalidateExistingOtpsAsync(string email);
}
