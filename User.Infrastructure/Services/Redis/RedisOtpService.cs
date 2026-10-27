namespace User.Infrastructure.Services.Redis;

using System;
using System.Collections.Concurrent;
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
    private readonly ConcurrentDictionary<string, OtpEntry> _inMemoryFallback = new(StringComparer.OrdinalIgnoreCase);
    private const string KeyPrefix = "otp:registration:";

    public RedisOtpService(
        ILogger<RedisOtpService> logger,
        IConnectionMultiplexer? redis = null)
    {
        _logger = logger;
        _redis = redis;
    }

    public async Task SaveOtpAsync(string email, string code, TimeSpan validityDuration, TimeSpan retentionDuration, string purpose = "registration")
    {
        var key = FormatKey(email, purpose);
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
            _logger.LogInformation("[REDIS] Stored OTP in Redis for '{Email}' | CODE: >>> {Code} <<< | Key: '{Key}' | TTL: {TtlSeconds}s.", email, code, key, retentionDuration.TotalSeconds);
        }
        else
        {
            _inMemoryFallback[key] = entry;
            _logger.LogWarning("[REDIS OFFLINE - IN-MEMORY FALLBACK] Stored OTP in memory for '{Email}'. CODE: >>> {Code} <<<", email, code);
        }
    }

    public async Task<OtpEntry?> GetOtpAsync(string email, string purpose = "registration")
    {
        var key = FormatKey(email, purpose);

        if (_redis != null && _redis.IsConnected)
        {
            var db = _redis.GetDatabase();
            var value = await db.StringGetAsync(key);
            if (value.IsNullOrEmpty)
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<OtpEntry>(value.ToString()!);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize OTP entry from Redis for key '{Key}'.", key);
                return null;
            }
        }

        if (_inMemoryFallback.TryGetValue(key, out var inMemoryEntry))
        {
            _logger.LogInformation("[REDIS OFFLINE - IN-MEMORY FALLBACK] Retrieved OTP from memory for '{Email}' | CODE: >>> {Code} <<<", email, inMemoryEntry.Code);
            return inMemoryEntry;
        }

        _logger.LogWarning("[REDIS OFFLINE] Cannot fetch OTP for '{Email}'. Returning null.", email);
        return null;
    }

    public async Task MarkOtpConsumedAsync(string email, string purpose = "registration")
    {
        var key = FormatKey(email, purpose);

        if (_redis != null && _redis.IsConnected)
        {
            var db = _redis.GetDatabase();
            var value = await db.StringGetAsync(key);
            if (!value.IsNullOrEmpty)
            {
                try
                {
                    var entry = JsonSerializer.Deserialize<OtpEntry>(value.ToString()!);
                    if (entry != null)
                    {
                        entry.Status = OtpStatus.Consumed;
                        var ttl = await db.KeyTimeToLiveAsync(key);
                        var remainingTtl = ttl ?? TimeSpan.FromMinutes(10);
                        if (remainingTtl <= TimeSpan.Zero) remainingTtl = TimeSpan.FromMinutes(1);

                        await db.StringSetAsync(key, JsonSerializer.Serialize(entry), remainingTtl);
                        _logger.LogInformation("Marked OTP as Consumed for '{Email}'.", email);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to mark OTP as consumed in Redis for '{Email}'.", email);
                }
            }
        }
        else
        {
            if (_inMemoryFallback.TryGetValue(key, out var inMemoryEntry))
            {
                inMemoryEntry.Status = OtpStatus.Consumed;
                _logger.LogInformation("[REDIS OFFLINE - IN-MEMORY FALLBACK] Marked OTP as Consumed in memory for '{Email}'.", email);
            }
        }
    }

    public async Task InvalidateExistingOtpsAsync(string email, string purpose = "registration")
    {
        var key = FormatKey(email, purpose);

        if (_redis != null && _redis.IsConnected)
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(key);
            _logger.LogInformation("Invalidated (deleted) existing OTPs in Redis for '{Email}'.", email);
        }
        else
        {
            _inMemoryFallback.TryRemove(key, out _);
            _logger.LogInformation("[REDIS OFFLINE - IN-MEMORY FALLBACK] Invalidated OTP in memory for '{Email}'.", email);
        }
    }

    private static string FormatKey(string email, string purpose = "registration") => $"otp:{purpose}:{email.Trim().ToLowerInvariant()}";
}
