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
/// T13: an order report is checked against Commerce before it is stored. The report must never be saved on an unverified reference,
/// and a Commerce outage must make the report fail with a retryable error instead of being stored as if it were verified.
/// </summary>
public sealed class ReportOrderVerificationTests
{
    private sealed class FakeCommerceClient(OrderReportCheck answer) : ICommerceServiceClient
    {
        public int Calls { get; private set; }

        public Task<OrderReportCheck> CheckOrderForReportAsync(Guid orderOrSubOrderId, Guid reporterId, Guid reportedUserId, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(answer);
        }

        public Task<bool> VerifyDeliveredOrderForDisputeAsync(Guid orderOrSubOrderId, Guid reporterId, Guid reportedUserId, CancellationToken cancellationToken = default) =>
            Task.FromResult(answer == OrderReportCheck.Eligible);
    }

    private sealed class FakeWalletClient : IWalletServiceClient
    {
        public List<(Guid UserId, string Reason)> Frozen { get; } = new();

        public Task<bool> FreezeWalletAsync(Guid userId, string reason, CancellationToken cancellationToken = default)
        {
            Frozen.Add((userId, reason));
            return Task.FromResult(true);
        }

        public Task<bool> UnfreezeWalletAsync(Guid userId, string reason, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private static async Task<(AppDbContext Db, Guid Reporter, Guid Seller)> CreateAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"report_order_verification_{Guid.NewGuid():N}")
            .Options;
        var db = new AppDbContext(options);
        var reporter = Guid.NewGuid();
        var seller = Guid.NewGuid();
        db.Users.AddRange(
            new User { Id = reporter, Email = "b@nexus.local", PasswordHash = "h", FirstName = "B", LastName = "1", Status = UserStatus.Active },
            new User { Id = seller, Email = "s@nexus.local", PasswordHash = "h", FirstName = "S", LastName = "1", Status = UserStatus.Active });
        await db.SaveChangesAsync();
        return (db, reporter, seller);
    }

    private static CreateReportRequest Request(Guid seller, ReportViolationType type, params string[] evidence) => new()
    {
        ReportedUserId = seller,
        TargetRole = TargetRole.Seller,
        ReferenceType = ReferenceType.Order,
        ReferenceId = Guid.NewGuid().ToString(),
        ViolationType = type,
        Description = "The seller sent an empty box in the parcel.",
        EvidenceUrls = new List<string>(evidence)
    };

    [Theory]
    [InlineData(OrderReportCheck.NotFound, "ORDER_NOT_FOUND", 404)]
    [InlineData(OrderReportCheck.NotParticipant, "ORDER_REPORT_NOT_ALLOWED", 403)]
    [InlineData(OrderReportCheck.NotDelivered, "ORDER_NOT_DELIVERED", 400)]
    [InlineData(OrderReportCheck.Unavailable, "ORDER_VERIFICATION_UNAVAILABLE", 503)]
    public async Task CreateReport_WhenOrderIsNotEligible_IsRefusedAndNothingIsStored(OrderReportCheck answer, string code, int status)
    {
        var (db, reporter, seller) = await CreateAsync();
        using var _ = db;
        var wallet = new FakeWalletClient();
        var service = new ReportService(db, NullLogger<ReportService>.Instance, walletServiceClient: wallet, commerceServiceClient: new FakeCommerceClient(answer));

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.CreateReportAsync(reporter, Request(seller, ReportViolationType.Fraud, "https://files/proof.png")));

        Assert.Equal(code, ex.ErrorCode);
        Assert.Equal(status, ex.StatusCode);
        Assert.Equal(0, await db.UserReports.CountAsync());
        Assert.Empty(wallet.Frozen);
    }

    [Theory]
    [InlineData(ReportViolationType.Fraud)]
    [InlineData(ReportViolationType.Counterfeit)]
    public async Task CreateReport_ForAGoodsDisputeWithoutEvidence_IsRefusedBeforeCommerceIsAsked(ReportViolationType type)
    {
        var (db, reporter, seller) = await CreateAsync();
        using var _ = db;
        var commerce = new FakeCommerceClient(OrderReportCheck.Eligible);
        var service = new ReportService(db, NullLogger<ReportService>.Instance, commerceServiceClient: commerce);

        var ex = await Assert.ThrowsAsync<DomainException>(() => service.CreateReportAsync(reporter, Request(seller, type)));

        Assert.Equal("EVIDENCE_REQUIRED", ex.ErrorCode);
        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(0, commerce.Calls);
        Assert.Equal(0, await db.UserReports.CountAsync());
    }

    [Fact]
    public async Task CreateReport_ForATypeThatDoesNotNeedEvidence_IsAcceptedWithoutFiles()
    {
        var (db, reporter, seller) = await CreateAsync();
        using var _ = db;
        var service = new ReportService(db, NullLogger<ReportService>.Instance, commerceServiceClient: new FakeCommerceClient(OrderReportCheck.Eligible));

        var report = await service.CreateReportAsync(reporter, Request(seller, ReportViolationType.Other));

        Assert.Equal("Pending", report.Status);
        Assert.Equal(1, await db.UserReports.CountAsync());
    }

    [Fact]
    public async Task CreateReport_WhenOrderIsEligible_IsStoredAndTheSellerWalletIsFrozen()
    {
        var (db, reporter, seller) = await CreateAsync();
        using var _ = db;
        var wallet = new FakeWalletClient();
        var service = new ReportService(db, NullLogger<ReportService>.Instance, walletServiceClient: wallet, commerceServiceClient: new FakeCommerceClient(OrderReportCheck.Eligible));

        var report = await service.CreateReportAsync(reporter, Request(seller, ReportViolationType.Fraud, "https://files/proof.png"));

        Assert.Equal("Pending", report.Status);
        Assert.Equal(1, await db.UserReports.CountAsync());
        Assert.Single(wallet.Frozen);
        Assert.Equal(seller, wallet.Frozen[0].UserId);
    }

    [Fact]
    public async Task CreateReport_WhenTheOrderReferenceIsNotAnId_IsRefusedWhenCommerceVerificationIsOn()
    {
        var (db, reporter, seller) = await CreateAsync();
        using var _ = db;
        var service = new ReportService(db, NullLogger<ReportService>.Instance, commerceServiceClient: new FakeCommerceClient(OrderReportCheck.Eligible));
        var request = new CreateReportRequest
        {
            ReportedUserId = seller,
            TargetRole = TargetRole.Seller,
            ReferenceType = ReferenceType.Order,
            ReferenceId = "ORD-NOT-A-GUID",
            ViolationType = ReportViolationType.Other,
            Description = "The seller never answered my messages."
        };

        var ex = await Assert.ThrowsAsync<DomainException>(() => service.CreateReportAsync(reporter, request));

        Assert.Equal("VAL_INVALID_INPUT", ex.ErrorCode);
    }
}
