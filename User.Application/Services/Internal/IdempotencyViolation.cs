namespace User.Application.Services.Internal;

using Microsoft.EntityFrameworkCore;

/// <summary>Recognises the failure of the unique index that guards reputation changes, without the Application layer depending on Npgsql.</summary>
public static class IdempotencyViolation
{
    public const string IndexName = "IX_ReputationLogs_Idempotency";

    public static bool IsReputationLogDuplicate(DbUpdateException exception)
    {
        for (var inner = exception.InnerException; inner != null; inner = inner.InnerException)
        {
            var type = inner.GetType();
            var sqlState = type.GetProperty("SqlState")?.GetValue(inner) as string;
            var constraint = type.GetProperty("ConstraintName")?.GetValue(inner) as string;
            if (sqlState == "23505" && constraint == IndexName)
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        for (var inner = exception.InnerException; inner != null; inner = inner.InnerException)
        {
            var sqlState = inner.GetType().GetProperty("SqlState")?.GetValue(inner) as string;
            if (sqlState == "23505")
            {
                return true;
            }
        }

        return false;
    }
}
