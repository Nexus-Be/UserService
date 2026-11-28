namespace User.Tests;

using System;
using Microsoft.EntityFrameworkCore;
using User.Application.Services.Internal;
using Xunit;

/// <summary>
/// T17: the unique index on reputation logs turns a double-applied penalty into a database error. Only that exact failure is treated as
/// "already applied"; any other database problem must still be reported as an error.
/// </summary>
public sealed class IdempotencyViolationTests
{
    // Stands in for Npgsql's PostgresException, which exposes the same two properties.
    private sealed class FakePostgresException(string sqlState, string constraintName) : Exception("fake database failure")
    {
        public string SqlState { get; } = sqlState;
        public string ConstraintName { get; } = constraintName;
    }

    [Fact]
    public void ADuplicateOnTheReputationIndex_IsRecognised()
    {
        var ex = new DbUpdateException("save failed", new FakePostgresException("23505", "IX_ReputationLogs_Idempotency"));

        Assert.True(IdempotencyViolation.IsReputationLogDuplicate(ex));
    }

    [Fact]
    public void ADuplicateOnAnotherIndex_IsNotTreatedAsAlreadyApplied()
    {
        var ex = new DbUpdateException("save failed", new FakePostgresException("23505", "IX_Users_Email"));

        Assert.False(IdempotencyViolation.IsReputationLogDuplicate(ex));
    }

    [Fact]
    public void AnotherDatabaseError_IsNotTreatedAsAlreadyApplied()
    {
        var ex = new DbUpdateException("save failed", new FakePostgresException("23503", "IX_ReputationLogs_Idempotency"));

        Assert.False(IdempotencyViolation.IsReputationLogDuplicate(ex));
    }

    [Fact]
    public void AnExceptionWithoutADatabaseCause_IsNotRecognised()
    {
        Assert.False(IdempotencyViolation.IsReputationLogDuplicate(new DbUpdateException("save failed")));
        Assert.False(IdempotencyViolation.IsReputationLogDuplicate(new DbUpdateException("save failed", new InvalidOperationException("other"))));
    }

    [Fact]
    public void TheCauseIsFoundWhenItIsNestedDeeper()
    {
        var ex = new DbUpdateException("save failed", new InvalidOperationException("wrapper", new FakePostgresException("23505", "IX_ReputationLogs_Idempotency")));

        Assert.True(IdempotencyViolation.IsReputationLogDuplicate(ex));
    }
}
