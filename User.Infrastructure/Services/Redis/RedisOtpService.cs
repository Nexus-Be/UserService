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
    private readonly ConcurrentDictionary<string, (int Count, DateTimeOffset ExpiresAt)> _inMemoryAttempts = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _inMemoryLockouts = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, (string Value, DateTimeOffset ExpiresAt)> _inMemoryDrafts = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _inMemoryCooldowns = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, (int Count, DateTimeOffset ExpiresAt)> _inMemoryOtpAttempts = new(StringComparer.OrdinalIgnoreCase);
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
        var attemptsKey = FormatOtpAttemptsKey(email, purpose);

        if (_redis != null && _redis.IsConnected)
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(new RedisKey[] { key, attemptsKey });
            _logger.LogInformation("Invalidated (deleted) existing OTPs in Redis for '{Email}'.", email);
        }
        else
        {
            _inMemoryFallback.TryRemove(key, out _);
            _inMemoryOtpAttempts.TryRemove(attemptsKey, out _);
            _logger.LogInformation("[REDIS OFFLINE - IN-MEMORY FALLBACK] Invalidated OTP in memory for '{Email}'.", email);
        }
    }

    public async Task<int> IncrementLoginAttemptsAsync(string identifier, TimeSpan window)
    {
        var key = FormatAttemptKey(identifier);
        if (_redis != null && _redis.IsConnected)
        {
            var db = _redis.GetDatabase();
            var count = await db.StringIncrementAsync(key);
            if (count == 1)
            {
                await db.KeyExpireAsync(key, window);
            }
            return (int)count;
        }

        var now = DateTimeOffset.UtcNow;
        var entry = _inMemoryAttempts.AddOrUpdate(
            key,
            _ => (1, now.Add(window)),
            (_, existing) => existing.ExpiresAt > now ? (existing.Count + 1, existing.ExpiresAt) : (1, now.Add(window))
        );
        return entry.Count;
    }

    public async Task<bool> IsAccountLockedAsync(string identifier)
    {
        var key = FormatLockoutKey(identifier);
        if (_redis != null && _redis.IsConnected)
        {
            var db = _redis.GetDatabase();
            return await db.KeyExistsAsync(key);
        }

        if (_inMemoryLockouts.TryGetValue(key, out var expiresAt))
        {
            if (expiresAt > DateTimeOffset.UtcNow)
            {
                return true;
            }
            _inMemoryLockouts.TryRemove(key, out _);
        }

        return false;
    }

    public async Task LockAccountAsync(string identifier, TimeSpan duration)
    {
        var key = FormatLockoutKey(identifier);
        if (_redis != null && _redis.IsConnected)
        {
            var db = _redis.GetDatabase();
            await db.StringSetAsync(key, "locked", duration);
            await db.KeyDeleteAsync(FormatAttemptKey(identifier));
            _logger.LogWarning("[AUTH] Account '{Identifier}' locked for {DurationMinutes} minutes in Redis.", identifier, duration.TotalMinutes);
        }
        else
        {
            _inMemoryLockouts[key] = DateTimeOffset.UtcNow.Add(duration);
            _inMemoryAttempts.TryRemove(FormatAttemptKey(identifier), out _);
            _logger.LogWarning("[REDIS OFFLINE - IN-MEMORY FALLBACK] Account '{Identifier}' locked for {DurationMinutes} minutes.", identifier, duration.TotalMinutes);
        }
    }

    public async Task ResetLoginAttemptsAsync(string identifier)
    {
        var attemptKey = FormatAttemptKey(identifier);
        var lockoutKey = FormatLockoutKey(identifier);

        if (_redis != null && _redis.IsConnected)
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(new RedisKey[] { attemptKey, lockoutKey });
        }
        else
        {
            _inMemoryAttempts.TryRemove(attemptKey, out _);
            _inMemoryLockouts.TryRemove(lockoutKey, out _);
        }
    }

    public async Task SaveDraftAsync(string key, string data, TimeSpan duration)
    {
        if (_redis != null && _redis.IsConnected)
        {
            var db = _redis.GetDatabase();
            await db.StringSetAsync(key, data, duration);
            _logger.LogInformation("[REDIS] Stored Draft in Redis for key '{Key}' | TTL: {TtlSeconds}s.", key, duration.TotalSeconds);
        }
        else
        {
            _inMemoryDrafts[key] = (data, DateTimeOffset.UtcNow.Add(duration));
            _logger.LogWarning("[REDIS OFFLINE - IN-MEMORY FALLBACK] Stored Draft in memory for key '{Key}'.", key);
        }
    }

    public async Task<string?> GetDraftAsync(string key)
    {
        if (_redis != null && _redis.IsConnected)
        {
            var db = _redis.GetDatabase();
            var val = await db.StringGetAsync(key);
            return val.IsNullOrEmpty ? null : val.ToString();
        }

        if (_inMemoryDrafts.TryGetValue(key, out var entry))
        {
            if (entry.ExpiresAt > DateTimeOffset.UtcNow)
            {
                return entry.Value;
            }
            _inMemoryDrafts.TryRemove(key, out _);
        }

        return null;
    }

    public async Task<string?> GetAndDeleteDraftAsync(string key)
    {
        if (_redis != null && _redis.IsConnected)
        {
            var db = _redis.GetDatabase();
            var val = await db.StringGetDeleteAsync(key);
            if (val.IsNullOrEmpty) return null;
            _logger.LogInformation("[REDIS] Atomically retrieved and deleted Draft for key '{Key}'.", key);
            return val.ToString();
        }

        if (_inMemoryDrafts.TryRemove(key, out var entry))
        {
            if (entry.ExpiresAt > DateTimeOffset.UtcNow)
            {
                _logger.LogInformation("[REDIS OFFLINE - IN-MEMORY FALLBACK] Retrieved and deleted Draft for key '{Key}'.", key);
                return entry.Value;
            }
        }

        return null;
    }

    public async Task<bool> IsCooldownActiveAsync(string email, string purpose = "registration")
    {
        var key = FormatCooldownKey(email, purpose);
        if (_redis != null && _redis.IsConnected)
        {
            var db = _redis.GetDatabase();
            return await db.KeyExistsAsync(key);
        }

        if (_inMemoryCooldowns.TryGetValue(key, out var expiresAt))
        {
            if (expiresAt > DateTimeOffset.UtcNow)
            {
                return true;
            }
            _inMemoryCooldowns.TryRemove(key, out _);
        }

        return false;
    }

    public async Task SetCooldownAsync(string email, TimeSpan duration, string purpose = "registration")
    {
        var key = FormatCooldownKey(email, purpose);
        if (_redis != null && _redis.IsConnected)
        {
            var db = _redis.GetDatabase();
            await db.StringSetAsync(key, "active", duration);
            _logger.LogInformation("[REDIS] Set OTP cooldown for '{Email}' (purpose: {Purpose}, TTL: {DurationSeconds}s).", email, purpose, duration.TotalSeconds);
        }
        else
        {
            _inMemoryCooldowns[key] = DateTimeOffset.UtcNow.Add(duration);
            _logger.LogInformation("[REDIS OFFLINE - IN-MEMORY FALLBACK] Set OTP cooldown for '{Email}' (purpose: {Purpose}).", email, purpose);
        }
    }

    public async Task<int> IncrementOtpAttemptsAsync(string email, TimeSpan window, string purpose = "registration")
    {
        var key = FormatOtpAttemptsKey(email, purpose);
        if (_redis != null && _redis.IsConnected)
        {
            var db = _redis.GetDatabase();
            var count = await db.StringIncrementAsync(key);
            if (count == 1)
            {
                await db.KeyExpireAsync(key, window);
            }
            _logger.LogInformation("[REDIS] Incremented OTP attempts for '{Email}' (purpose: {Purpose}, count: {Count}).", email, purpose, count);
            return (int)count;
        }

        var now = DateTimeOffset.UtcNow;
        var entry = _inMemoryOtpAttempts.AddOrUpdate(
            key,
            _ => (1, now.Add(window)),
            (_, existing) => existing.ExpiresAt > now ? (existing.Count + 1, existing.ExpiresAt) : (1, now.Add(window))
        );
        _logger.LogInformation("[REDIS OFFLINE - IN-MEMORY FALLBACK] Incremented OTP attempts for '{Email}' (purpose: {Purpose}, count: {Count}).", email, purpose, entry.Count);
        return entry.Count;
    }

    public async Task ResetOtpAttemptsAsync(string email, string purpose = "registration")
    {
        var key = FormatOtpAttemptsKey(email, purpose);
        if (_redis != null && _redis.IsConnected)
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(key);
            _logger.LogInformation("[REDIS] Reset OTP attempts for '{Email}' (purpose: {Purpose}).", email, purpose);
        }
        else
        {
            _inMemoryOtpAttempts.TryRemove(key, out _);
            _logger.LogInformation("[REDIS OFFLINE - IN-MEMORY FALLBACK] Reset OTP attempts for '{Email}' (purpose: {Purpose}).", email, purpose);
        }
    }

    private static string FormatKey(string email, string purpose = "registration") => $"otp:{purpose}:{email.Trim().ToLowerInvariant()}";
    private static string FormatCooldownKey(string email, string purpose) => $"otp:cooldown:{purpose}:{email.Trim().ToLowerInvariant()}";
    private static string FormatOtpAttemptsKey(string email, string purpose) => $"otp:attempts:{purpose}:{email.Trim().ToLowerInvariant()}";
    private static string FormatAttemptKey(string identifier) => $"user:login_attempts:{identifier.Trim().ToLowerInvariant()}";
    private static string FormatLockoutKey(string identifier) => $"user:login_lockout:{identifier.Trim().ToLowerInvariant()}";
}
