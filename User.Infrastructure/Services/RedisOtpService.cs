namespace User.Infrastructure.Services;

using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using User.Application.Interfaces;
using User.Application.Models;

public sealed class RedisOtpService : IRedisOtpService
{
    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<RedisOtpService> _logger;
    private const string KeyPrefix = "otp:registration:";

    public RedisOtpService(
        ILogger<RedisOtpService> logger,
        IConnectionMultiplexer? redis = null)
    {
        _logger = logger;
        _redis = redis;
    }

    public async Task SaveOtpAsync(string email, string code, TimeSpan validityDuration, TimeSpan retentionDuration)
    {
        var key = FormatKey(email);
        var entry = new OtpEntry
        {
            Code = code,
            Email = email,
            Status = OtpStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.Add(validityDuration)
        };

        var json = JsonSerializer.Serialize(entry);

        if (_redis != null && _redis.IsConnected)
        {
            var db = _redis.GetDatabase();
            await db.StringSetAsync(key, json, retentionDuration);
            _logger.LogInformation("Stored OTP in Redis for '{Email}' with TTL {TtlSeconds}s.", email, retentionDuration.TotalSeconds);
        }
        else
        {
            _logger.LogWarning("[REDIS OFFLINE] Simulated storing OTP for '{Email}'. Code: {Code}", email, code);
        }
    }

    public async Task<OtpEntry?> GetOtpAsync(string email)
    {
        var key = FormatKey(email);
        if (_redis != null && _redis.IsConnected)
        {
            var db = _redis.GetDatabase();
            var val = await db.StringGetAsync(key);
            if (!val.HasValue) return null;

            return JsonSerializer.Deserialize<OtpEntry>(val.ToString()!);
        }

        return null;
    }

    public async Task MarkOtpConsumedAsync(string email)
    {
        var key = FormatKey(email);
        if (_redis != null && _redis.IsConnected)
        {
            var db = _redis.GetDatabase();
            var val = await db.StringGetAsync(key);
            if (val.HasValue)
            {
                var entry = JsonSerializer.Deserialize<OtpEntry>(val.ToString()!);
                if (entry != null)
                {
                    entry.Status = OtpStatus.Consumed;
                    entry.ConsumedAt = DateTimeOffset.UtcNow;
                    var remainingTtl = await db.KeyTimeToLiveAsync(key);
                    var ttl = remainingTtl ?? TimeSpan.FromMinutes(10);
                    await db.StringSetAsync(key, JsonSerializer.Serialize(entry), ttl);
                    _logger.LogInformation("Marked OTP as Consumed in Redis for '{Email}'.", email);
                }
            }
        }
    }

    public async Task InvalidateExistingOtpsAsync(string email)
    {
        var key = FormatKey(email);
        if (_redis != null && _redis.IsConnected)
        {
            var db = _redis.GetDatabase();
            var val = await db.StringGetAsync(key);
            if (val.HasValue)
            {
                var entry = JsonSerializer.Deserialize<OtpEntry>(val.ToString()!);
                if (entry != null && entry.Status == OtpStatus.Active)
                {
                    entry.Status = OtpStatus.Expired;
                    var remainingTtl = await db.KeyTimeToLiveAsync(key);
                    var ttl = remainingTtl ?? TimeSpan.FromMinutes(5);
                    await db.StringSetAsync(key, JsonSerializer.Serialize(entry), ttl);
                    _logger.LogInformation("Invalidated previous active OTP for '{Email}'.", email);
                }
            }
        }
    }

    private static string FormatKey(string email) => $"{KeyPrefix}{email.Trim().ToLowerInvariant()}";
}
