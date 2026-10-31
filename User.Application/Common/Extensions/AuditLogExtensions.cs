namespace User.Application.Common.Extensions;

using System;
using System.Text.Json;
using User.Application.Interfaces;
using User.Domain.Entities;
using User.Domain.Enums;

public static class AuditLogExtensions
{
    public static AuditLog AddAuditLog(
        this IUserDbContext dbContext,
        AuditAction action,
        string entityName,
        string entityId,
        object? oldValues = null,
        object? newValues = null,
        string? userId = null,
        string? userName = null,
        object? changedColumns = null,
        string? ipAddress = null,
        string? userAgent = null,
        string? traceId = null,
        string serviceName = "user-service")
    {
        var log = new AuditLog
        {
            Id = Guid.NewGuid(),
            ServiceName = serviceName,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            UserId = userId,
            UserName = userName,
            OldValues = SerializePayload(oldValues),
            NewValues = SerializePayload(newValues),
            ChangedColumns = SerializePayload(changedColumns),
            IpAddress = ipAddress,
            UserAgent = userAgent,
            TraceId = traceId,
            CreatedAt = DateTimeOffset.UtcNow,
            IsSynced = false
        };

        dbContext.AuditLogs.Add(log);
        return log;
    }

    private static string? SerializePayload(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is string str)
        {
            return str;
        }

        return JsonSerializer.Serialize(value);
    }
}
