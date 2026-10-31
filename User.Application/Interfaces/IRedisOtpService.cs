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

    Task<int> IncrementLoginAttemptsAsync(string identifier, TimeSpan window);
    Task<bool> IsAccountLockedAsync(string identifier);
    Task LockAccountAsync(string identifier, TimeSpan duration);
    Task ResetLoginAttemptsAsync(string identifier);

    Task SaveDraftAsync(string key, string data, TimeSpan duration) => Task.CompletedTask;
    Task<string?> GetDraftAsync(string key) => Task.FromResult<string?>(null);
    Task<string?> GetAndDeleteDraftAsync(string key) => Task.FromResult<string?>(null);

    Task<bool> IsCooldownActiveAsync(string email, string purpose = "registration") => Task.FromResult(false);
    Task SetCooldownAsync(string email, TimeSpan duration, string purpose = "registration") => Task.CompletedTask;
    Task<int> IncrementOtpAttemptsAsync(string email, TimeSpan window, string purpose = "registration") => Task.FromResult(1);
    Task ResetOtpAttemptsAsync(string email, string purpose = "registration") => Task.CompletedTask;
}
