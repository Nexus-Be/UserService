namespace User.Tests;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using User.Application.Interfaces;
using User.Application.Services.Report;
using User.Domain.Entities;
using User.Domain.Enums;
using User.Domain.Exceptions;
using User.Infrastructure.Persistence;
using Xunit;

/// <summary>
/// T15: resolving an order report in the buyer's favour asks Commerce for the refund, never sends an amount, and stores what happened.
/// "Refunded" is written only after Commerce confirmed it; a failure or a manual case stays visible to staff and can be retried.
/// </summary>
public sealed class ReportRefundTests
{
    private sealed class ScriptedCommerce : ICommerceServiceClient
    {
        public Queue<DisputeRefundResult> Answers { get; } = new();
        public List<(Guid SubOrderId, Guid ReportId)> Calls { get; } = new();

        public Task<DisputeRefundResult> RequestDisputeRefundAsync(Guid subOrderId, Guid reportId, string? note, CancellationToken cancellationToken = default)
        {
            Calls.Add((subOrderId, reportId));
            return Task.FromResult(Answers.Count > 0 ? Answers.Dequeue() : new DisputeRefundResult(DisputeRefundOutcome.Failed, 0m, "no answer"));
        }

        public Task<OrderReportCheck> CheckOrderForReportAsync(Guid orderOrSubOrderId, Guid reporterId, Guid reportedUserId, CancellationToken cancellationToken = default) =>
            Task.FromResult(OrderReportCheck.Eligible);

        public Task<bool> VerifyDeliveredOrderForDisputeAsync(Guid orderOrSubOrderId, Guid reporterId, Guid reportedUserId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    private static async Task<(AppDbContext Db, ReportService Service, ScriptedCommerce Commerce, Guid Report, Guid Staff)> SetUpAsync(ReferenceType referenceType = ReferenceType.Order, string? referenceId = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"report_refund_{Guid.NewGuid():N}").Options;
        var db = new AppDbContext(options);
        var buyer = Guid.NewGuid();
        var seller = Guid.NewGuid();
        db.Users.AddRange(
            new User { Id = buyer, Email = "b@nexus.local", PasswordHash = "h", FirstName = "B", LastName = "1", Status = UserStatus.Active },
            new User { Id = seller, Email = "s@nexus.local", PasswordHash = "h", FirstName = "S", LastName = "1", Status = UserStatus.Active });
        var report = new UserReport
        {
            Id = Guid.NewGuid(), ReporterId = buyer, ReportedUserId = seller, TargetRole = TargetRole.Seller,
            ReferenceType = referenceType, ReferenceId = referenceId ?? Guid.NewGuid().ToString(),
            ViolationType = ReportViolationType.Fraud, Description = "The seller sent an empty box.", Status = ReportStatus.Pending
        };
        db.UserReports.Add(report);
        await db.SaveChangesAsync();
        var commerce = new ScriptedCommerce();
        return (db, new ReportService(db, NullLogger<ReportService>.Instance, commerceServiceClient: commerce), commerce, report.Id, Guid.NewGuid());
    }

    private static ResolveReportRequest Resolve(bool refund) => new()
    {
        TargetRole = TargetRole.Seller,
        RefundBuyer = refund,
        ResolutionNote = "Evidence confirms the seller shipped an empty box."
    };

    [Fact]
    public async Task Resolve_WithRefund_WhenCommerceConfirms_StoresRefunded_AndTheAmountCommerceWorkedOut()
    {
        var (db, service, commerce, report, staff) = await SetUpAsync();
        using var _ = db;
        commerce.Answers.Enqueue(new DisputeRefundResult(DisputeRefundOutcome.Refunded, 250000m, null));

        var result = await service.ResolveReportAsync(report, Resolve(true), staff);

        Assert.Equal("Resolved", result.Status);
        Assert.Equal("Refunded", result.RefundStatus);
        Assert.Equal(250000m, result.RefundAmount);
        Assert.Single(commerce.Calls);
        Assert.Equal(report, commerce.Calls[0].ReportId);
        Assert.Equal(ReportRefundStatus.Refunded, (await db.UserReports.SingleAsync()).RefundStatus);
    }

    [Fact]
    public async Task Resolve_WithoutRefund_NeverCallsCommerce()
    {
        var (db, service, commerce, report, staff) = await SetUpAsync();
        using var _ = db;

        var result = await service.ResolveReportAsync(report, Resolve(false), staff);

        Assert.Equal("None", result.RefundStatus);
        Assert.Empty(commerce.Calls);
    }

    [Theory]
    [InlineData(DisputeRefundOutcome.ManualProcessingRequired, "ManualProcessingRequired")]
    [InlineData(DisputeRefundOutcome.Failed, "Failed")]
    public async Task Resolve_WhenTheRefundCannotBeMade_KeepsTheDecision_AndShowsWhyForStaff(DisputeRefundOutcome outcome, string expected)
    {
        var (db, service, commerce, report, staff) = await SetUpAsync();
        using var _ = db;
        commerce.Answers.Enqueue(new DisputeRefundResult(outcome, 0m, "see staff"));

        var result = await service.ResolveReportAsync(report, Resolve(true), staff);

        Assert.Equal("Resolved", result.Status);
        Assert.Equal(expected, result.RefundStatus);
        Assert.Equal("see staff", result.RefundMessage);
    }

    [Fact]
    public async Task Retry_AfterAFailure_CallsCommerceAgain_AndCanSucceed()
    {
        var (db, service, commerce, report, staff) = await SetUpAsync();
        using var _ = db;
        commerce.Answers.Enqueue(new DisputeRefundResult(DisputeRefundOutcome.Failed, 0m, "wallet down"));
        commerce.Answers.Enqueue(new DisputeRefundResult(DisputeRefundOutcome.Refunded, 250000m, null));
        await service.ResolveReportAsync(report, Resolve(true), staff);

        var retried = await service.RetryReportRefundAsync(report, staff);

        Assert.Equal("Refunded", retried.RefundStatus);
        Assert.Equal(2, commerce.Calls.Count);
        Assert.Equal(commerce.Calls[0].ReportId, commerce.Calls[1].ReportId);
    }

    [Fact]
    public async Task Retry_WhenAlreadyRefunded_IsRefused()
    {
        var (db, service, commerce, report, staff) = await SetUpAsync();
        using var _ = db;
        commerce.Answers.Enqueue(new DisputeRefundResult(DisputeRefundOutcome.Refunded, 250000m, null));
        await service.ResolveReportAsync(report, Resolve(true), staff);

        var ex = await Assert.ThrowsAsync<DomainException>(() => service.RetryReportRefundAsync(report, staff));

        Assert.Equal("REFUND_NOT_RETRYABLE", ex.ErrorCode);
        Assert.Single(commerce.Calls);
    }

    [Fact]
    public async Task Resolve_WithRefund_ForAReportThatIsNotAboutAnOrder_IsRefusedBeforeAnythingIsDecided()
    {
        var (db, service, commerce, report, staff) = await SetUpAsync(ReferenceType.Auction);
        using var _ = db;

        var ex = await Assert.ThrowsAsync<DomainException>(() => service.ResolveReportAsync(report, Resolve(true), staff));

        Assert.Equal("REFUND_NOT_APPLICABLE", ex.ErrorCode);
        Assert.Equal(ReportStatus.Pending, (await db.UserReports.SingleAsync()).Status);
        Assert.Empty(commerce.Calls);
    }

    [Fact]
    public async Task AdminList_CanBeFilteredByRefundStatus_ToFindTheCasesThatNeedAPerson()
    {
        var (db, service, commerce, report, staff) = await SetUpAsync();
        using var _ = db;
        commerce.Answers.Enqueue(new DisputeRefundResult(DisputeRefundOutcome.ManualProcessingRequired, 0m, "seller already paid"));
        await service.ResolveReportAsync(report, Resolve(true), staff);

        var manual = await service.GetAdminReportsAsync(refundStatus: ReportRefundStatus.ManualProcessingRequired);
        var refunded = await service.GetAdminReportsAsync(refundStatus: ReportRefundStatus.Refunded);

        Assert.Single(manual.Items);
        Assert.Equal("ManualProcessingRequired", manual.Items[0].RefundStatus);
        Assert.Empty(refunded.Items);
    }
}
