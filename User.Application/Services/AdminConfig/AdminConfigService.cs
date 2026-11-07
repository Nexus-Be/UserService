namespace User.Application.Services.AdminConfig;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using User.Application.Common;
using User.Application.Common.Extensions;
using User.Application.Common.Models;
using User.Application.Interfaces;
using User.Domain.Entities;
using User.Domain.Enums;
using User.Domain.Exceptions;

public sealed class AdminConfigService : IAdminConfigService
{
    private readonly IUserDbContext _dbContext;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AdminConfigService> _logger;

    public AdminConfigService(
        IUserDbContext dbContext,
        IMemoryCache cache,
        ILogger<AdminConfigService> logger)
    {
        _dbContext = dbContext;
        _cache = cache;
        _logger = logger;
    }

    public async Task<PagedData<SystemConfigItemDto>> GetConfigsAsync(
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var (safePage, safeLimit) = PaginationConstants.Normalize(page, pageSize);

        var query = _dbContext.SystemConfigs.AsNoTracking();
        var totalCount = await query.CountAsync(cancellationToken);

        var rawItems = await query
            .OrderBy(c => c.ConfigKey)
            .Skip((safePage - 1) * safeLimit)
            .Take(safeLimit)
            .ToListAsync(cancellationToken);

        var items = rawItems.Select(c => new SystemConfigItemDto
        {
            Key = c.ConfigKey,
            Value = c.ConfigValue,
            Description = c.Description,
            DataType = c.DataType.ToEnumString(),
            UpdatedAt = c.UpdatedAt
        }).ToList();

        return PagedData<SystemConfigItemDto>.From(items, safePage, safeLimit, totalCount);
    }

    public async Task<UpdateSystemConfigResponse> UpdateConfigAsync(
        UpdateSystemConfigRequest request,
        Guid adminId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Key) || string.IsNullOrWhiteSpace(request.Value))
        {
            throw new DomainException("VAL_INVALID_INPUT", "Configuration key and value are required.", 400);
        }

        var normalizedKey = request.Key.Trim();
        var normalizedValue = request.Value.Trim();

        var config = await _dbContext.SystemConfigs
            .FirstOrDefaultAsync(c => c.ConfigKey == normalizedKey, cancellationToken);

        if (config == null)
        {
            throw new DomainException("CONFIG_NOT_FOUND", "Configuration key was not found.", 404);
        }

        ValidateConfigValueByType(config.DataType, normalizedValue);

        var oldValue = config.ConfigValue;
        var now = DateTimeOffset.UtcNow;

        config.ConfigValue = normalizedValue;
        config.UpdatedBy = adminId;
        config.UpdatedAt = now;

        _dbContext.AddAuditLog(
            action: AuditAction.SystemConfigUpdate,
            entityName: nameof(SystemConfig),
            entityId: config.Id.ToString(),
            oldValues: new { Key = config.ConfigKey, Value = oldValue },
            newValues: new { Key = config.ConfigKey, Value = normalizedValue, Reason = request.Reason?.Trim() },
            userId: adminId.ToString(),
            changedColumns: new[] { nameof(SystemConfig.ConfigValue), nameof(SystemConfig.UpdatedBy), nameof(SystemConfig.UpdatedAt) });

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Invalidate IMemoryCache entry used by InternalUserService
        _cache.Remove($"sys_config:{config.ConfigKey}");

        _logger.LogInformation(
            "[AdminConfigService] Updated config '{Key}' from '{OldValue}' to '{NewValue}' by Admin '{AdminId}'",
            config.ConfigKey, oldValue, normalizedValue, adminId);

        return new UpdateSystemConfigResponse
        {
            Key = config.ConfigKey,
            OldValue = oldValue,
            NewValue = normalizedValue,
            UpdatedAt = now
        };
    }

    private static void ValidateConfigValueByType(ConfigDataType dataType, string value)
    {
        switch (dataType)
        {
            case ConfigDataType.Int:
                if (!int.TryParse(value, out _))
                {
                    throw new DomainException("INVALID_CONFIG_VALUE", "Configuration parameter value must be a valid integer.", 400);
                }
                break;

            case ConfigDataType.Boolean:
                if (!bool.TryParse(value, out _))
                {
                    throw new DomainException("INVALID_CONFIG_VALUE", "Configuration parameter value must be a valid boolean (true or false).", 400);
                }
                break;
        }
    }
}
