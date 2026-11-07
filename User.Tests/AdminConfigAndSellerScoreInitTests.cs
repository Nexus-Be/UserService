namespace User.Tests;

using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using User.Application.Services.AdminConfig;
using User.Domain.Entities;
using User.Domain.Enums;
using User.Domain.Exceptions;
using User.Infrastructure.Persistence;
using Xunit;

public sealed class AdminConfigAndSellerScoreInitTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"admin_config_test_db_{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task UpdateConfigAsync_UpdatesValue_InvalidatesCache_AndWritesAuditLog()
    {
        using var dbContext = CreateInMemoryDbContext();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new AdminConfigService(dbContext, cache, NullLogger<AdminConfigService>.Instance);

        var adminId = Guid.NewGuid();
        dbContext.SystemConfigs.Add(new SystemConfig
        {
            Id = Guid.NewGuid(),
            ConfigKey = "AUCTION_MIN_REPUTATION_BID",
            ConfigValue = "40",
            DataType = ConfigDataType.Int,
            Description = "Min bid score"
        });
        await dbContext.SaveChangesAsync();

        cache.Set("sys_config:AUCTION_MIN_REPUTATION_BID", 40);

        var updated = await service.UpdateConfigAsync(new UpdateSystemConfigRequest
        {
            Key = "AUCTION_MIN_REPUTATION_BID",
            Value = "45",
            Reason = "Tighten anti-default bidding threshold during peak event."
        }, adminId);

        Assert.Equal("AUCTION_MIN_REPUTATION_BID", updated.Key);
        Assert.Equal("40", updated.OldValue);
        Assert.Equal("45", updated.NewValue);

        // Cache was invalidated
        Assert.False(cache.TryGetValue("sys_config:AUCTION_MIN_REPUTATION_BID", out _));

        var audit = await dbContext.AuditLogs.FirstOrDefaultAsync(a => a.Action == AuditAction.SystemConfigUpdate);
        Assert.NotNull(audit);
    }

    [Fact]
    public async Task UpdateConfigAsync_WhenInvalidDataTypeOrMissingKey_ThrowsExpectedDomainException()
    {
        using var dbContext = CreateInMemoryDbContext();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new AdminConfigService(dbContext, cache, NullLogger<AdminConfigService>.Instance);

        dbContext.SystemConfigs.Add(new SystemConfig
        {
            Id = Guid.NewGuid(),
            ConfigKey = "AUCTION_MIN_REPUTATION_BID",
            ConfigValue = "40",
            DataType = ConfigDataType.Int,
            Description = "Min bid score"
        });
        await dbContext.SaveChangesAsync();

        var invalidValEx = await Assert.ThrowsAsync<DomainException>(() =>
            service.UpdateConfigAsync(new UpdateSystemConfigRequest
            {
                Key = "AUCTION_MIN_REPUTATION_BID",
                Value = "not_an_integer",
                Reason = "Testing invalid integer config value."
            }, Guid.NewGuid()));

        Assert.Equal("INVALID_CONFIG_VALUE", invalidValEx.ErrorCode);

        var notFoundEx = await Assert.ThrowsAsync<DomainException>(() =>
            service.UpdateConfigAsync(new UpdateSystemConfigRequest
            {
                Key = "NON_EXISTENT_CONFIG_KEY",
                Value = "10",
                Reason = "Testing missing config key."
            }, Guid.NewGuid()));

        Assert.Equal("CONFIG_NOT_FOUND", notFoundEx.ErrorCode);
        Assert.Equal(404, notFoundEx.StatusCode);
    }
}
