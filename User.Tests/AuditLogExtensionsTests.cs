namespace User.Tests;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using User.Application.Common.Extensions;
using User.Domain.Enums;
using User.Infrastructure.Persistence;
using Xunit;

public sealed class AuditLogExtensionsTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"AuditLogTestsDb_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task AddAuditLog_WithObjectPayloads_SerializesToJsonAndAddsToDbContext()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var entityId = Guid.NewGuid().ToString();
        var userId = Guid.NewGuid().ToString();
        var oldValues = new { Status = "Pending", Role = "Buyer" };
        var newValues = new { Status = "Active", Role = "Seller" };
        var changedColumns = new[] { "Status", "Role" };

        // Act
        var result = dbContext.AddAuditLog(
            action: AuditAction.RoleAssign,
            entityName: "User",
            entityId: entityId,
            oldValues: oldValues,
            newValues: newValues,
            userId: userId,
            userName: "admin_user",
            changedColumns: changedColumns);

        await dbContext.SaveChangesAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("user-service", result.ServiceName);
        Assert.Equal(AuditAction.RoleAssign, result.Action);
        Assert.Equal("User", result.EntityName);
        Assert.Equal(entityId, result.EntityId);
        Assert.Equal(userId, result.UserId);
        Assert.Equal("admin_user", result.UserName);
        Assert.True(result.CreatedAt <= DateTimeOffset.UtcNow);

        // Verify JSON contents
        Assert.NotNull(result.OldValues);
        using var oldDoc = JsonDocument.Parse(result.OldValues);
        Assert.Equal("Pending", oldDoc.RootElement.GetProperty("Status").GetString());

        Assert.NotNull(result.NewValues);
        using var newDoc = JsonDocument.Parse(result.NewValues);
        Assert.Equal("Active", newDoc.RootElement.GetProperty("Status").GetString());

        Assert.NotNull(result.ChangedColumns);
        using var colDoc = JsonDocument.Parse(result.ChangedColumns);
        Assert.Equal(2, colDoc.RootElement.GetArrayLength());

        // Verify persisted in DB
        var persisted = await dbContext.AuditLogs.FirstOrDefaultAsync(x => x.Id == result.Id);
        Assert.NotNull(persisted);
        Assert.Equal(entityId, persisted.EntityId);
    }

    [Fact]
    public async Task AddAuditLog_WithStringPayloads_PreservesRawStringsWithoutDoubleSerialization()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        const string rawJson = "{\"customKey\":\"customValue\"}";

        // Act
        var result = dbContext.AddAuditLog(
            action: AuditAction.SystemConfigUpdate,
            entityName: "SystemConfig",
            entityId: "1",
            oldValues: rawJson,
            newValues: rawJson);

        await dbContext.SaveChangesAsync();

        // Assert
        Assert.Equal(rawJson, result.OldValues);
        Assert.Equal(rawJson, result.NewValues);
    }

    [Fact]
    public async Task AddAuditLog_WithNullPayloads_StoresNullValues()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();

        // Act
        var result = dbContext.AddAuditLog(
            action: AuditAction.UserUnban,
            entityName: "User",
            entityId: "123",
            oldValues: null,
            newValues: null,
            changedColumns: null);

        await dbContext.SaveChangesAsync();

        // Assert
        Assert.Null(result.OldValues);
        Assert.Null(result.NewValues);
        Assert.Null(result.ChangedColumns);
        Assert.Null(result.IpAddress);
        Assert.Null(result.UserAgent);
        Assert.Null(result.TraceId);
    }

    [Fact]
    public async Task AddAuditLog_WithNetworkAndTraceDetails_StoresContextFields()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();

        // Act
        var result = dbContext.AddAuditLog(
            action: AuditAction.UserBan,
            entityName: "User",
            entityId: "target-456",
            ipAddress: "192.168.1.100",
            userAgent: "Mozilla/5.0 TestBrowser",
            traceId: "trace-abc-123",
            serviceName: "custom-user-service");

        await dbContext.SaveChangesAsync();

        // Assert
        Assert.Equal("192.168.1.100", result.IpAddress);
        Assert.Equal("Mozilla/5.0 TestBrowser", result.UserAgent);
        Assert.Equal("trace-abc-123", result.TraceId);
        Assert.Equal("custom-user-service", result.ServiceName);
    }
}
