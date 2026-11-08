namespace User.Application.Common.Extensions;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using User.Application.Interfaces;
using User.Domain.Entities;
using User.Domain.Enums;

public static class AuditLogExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    private static readonly HashSet<string> SensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "password",
        "passwordhash",
        "newpassword",
        "oldpassword",
        "currentpassword",
        "confirmpassword",
        "token",
        "refreshtoken",
        "accesstoken",
        "secret",
        "clientsecret",
        "otp",
        "code",
        "pin",
        "cvv"
    };

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
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);

        var log = new AuditLog
        {
            Id = Guid.NewGuid(),
            ServiceName = string.IsNullOrWhiteSpace(serviceName) ? "user-service" : serviceName,
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

        try
        {
            var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
            var sanitized = SanitizeObject(value, visited);
            return JsonSerializer.Serialize(sanitized, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static object? SanitizeObject(object? obj, HashSet<object> visited)
    {
        if (obj is null)
        {
            return null;
        }

        var type = obj.GetType();
        if (type.IsPrimitive || obj is string || obj is decimal || obj is DateTime || obj is DateTimeOffset || obj is TimeSpan || obj is Guid || type.IsEnum)
        {
            return obj;
        }

        if (!visited.Add(obj))
        {
            return null;
        }

        if (obj is IDictionary dict)
        {
            var sanitizedDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in dict)
            {
                var key = entry.Key?.ToString() ?? string.Empty;
                sanitizedDict[key] = IsSensitive(key) ? "***REDACTED***" : SanitizeObject(entry.Value, visited);
            }
            return sanitizedDict;
        }

        if (obj is IEnumerable list and not string)
        {
            var sanitizedList = new List<object?>();
            foreach (var item in list)
            {
                sanitizedList.Add(SanitizeObject(item, visited));
            }
            return sanitizedList;
        }

        var properties = type.GetProperties();
        if (properties.Length > 0)
        {
            var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in properties)
            {
                if (!prop.CanRead || prop.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                var val = prop.GetValue(obj);
                result[prop.Name] = IsSensitive(prop.Name) ? "***REDACTED***" : SanitizeObject(val, visited);
            }
            return result;
        }

        return obj;
    }

    private static bool IsSensitive(string key) => SensitiveKeys.Contains(key);
}
