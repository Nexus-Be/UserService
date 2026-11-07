namespace User.Tests;

using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using User.Application.Services.Report;
using User.Domain.Entities;
using User.Domain.Enums;
using User.Domain.Exceptions;
using User.Infrastructure.Persistence;
using Xunit;

public sealed class ReportServiceTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"report_service_test_db_{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task CreateAndResolveReportAsync_WhenSellerViolates_DeductsSellerScoreAndCreatesPenaltyAndAuditLog()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new ReportService(dbContext, NullLogger<ReportService>.Instance);

        var reporterId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var staffId = Guid.NewGuid();

        dbContext.Users.AddRange(
            new User { Id = reporterId, Email = "buyer@nexus.local", PasswordHash = "h", FirstName = "Buyer", LastName = "Reporter", Status = UserStatus.Active },
            new User { Id = sellerId, Email = "seller@nexus.local", PasswordHash = "h", FirstName = "Seller", LastName = "Reported", Status = UserStatus.Active });

        dbContext.SellerProfiles.Add(new SellerProfile
        {
            Id = Guid.NewGuid(),
            UserId = sellerId,
            ShopName = "Violating Shop",
            PickupAddress = "123 Street",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "W1",
            Hotline = "0900000000",
            Status = SellerProfileStatus.Approved
        });

        dbContext.ReputationScores.Add(new ReputationScore
        {
            Id = Guid.NewGuid(),
            UserId = sellerId,
            CurrentScore = 70,
            TrustLevel = TrustLevel.Good,
            SellerScore = 52,
            SellerTrustLevel = TrustLevel.Standard
        });
        await dbContext.SaveChangesAsync();

        // 1. Buyer submits a report against Seller
        var report = await service.CreateReportAsync(reporterId, new CreateReportRequest
        {
            ReportedUserId = sellerId,
            TargetRole = TargetRole.Seller,
            ReferenceType = ReferenceType.Order,
            ReferenceId = "ORD-FRAUD-99",
            ViolationType = ReportViolationType.Counterfeit,
            Description = "Seller shipped counterfeit replica headphones instead of authentic product."
        });

        Assert.Equal("Pending", report.Status);
        Assert.Equal("Seller", report.TargetRole);

        // 2. Duplicate report for same reference while pending is blocked
        var dupEx = await Assert.ThrowsAsync<DomainException>(() =>
            service.CreateReportAsync(reporterId, new CreateReportRequest
            {
                ReportedUserId = sellerId,
                TargetRole = TargetRole.Seller,
                ReferenceType = ReferenceType.Order,
                ReferenceId = "ORD-FRAUD-99",
                ViolationType = ReportViolationType.Counterfeit,
                Description = "Submitting same report again."
            }));
        Assert.Equal("DUPLICATE_REPORT", dupEx.ErrorCode);

        // 3. Staff resolves report with 10-point SellerScore deduction and Seller role restriction
        var resolved = await service.ResolveReportAsync(
            report.ReportId,
            new ResolveReportRequest
            {
                TargetRole = TargetRole.Seller,
                PenaltyApplied = PenaltyType.Restriction,
                ScoreDeduction = 10,
                SuspendSellerShop = true,
                DurationDays = 14,
                PolicyClauseCode = "TOS_COUNTERFEIT_3_2",
                ResolutionNote = "Confirmed counterfeit shipment via unboxing video evidence."
            },
            staffId);

        Assert.Equal("Resolved", resolved.Status);
        Assert.Equal(10, resolved.ScoreDeducted);
        Assert.Equal(42, resolved.ReportedUserNewScore);

        var scoreInDb = await dbContext.ReputationScores.FirstAsync(r => r.UserId == sellerId);
        Assert.Equal(70, scoreInDb.CurrentScore); // Buyer score untouched!
        Assert.Equal(42, scoreInDb.SellerScore);  // Seller score dropped from 52 to 42!
        Assert.Equal(TrustLevel.Probation, scoreInDb.SellerTrustLevel);

        var shopInDb = await dbContext.SellerProfiles.FirstAsync(s => s.UserId == sellerId);
        Assert.Equal(SellerProfileStatus.Suspended, shopInDb.Status);

        var penaltyInDb = await dbContext.UserPenalties.FirstOrDefaultAsync(p => p.UserId == sellerId);
        Assert.NotNull(penaltyInDb);
        Assert.Equal(TargetRole.Seller, penaltyInDb.TargetRole);
        Assert.Equal(10, penaltyInDb.PointsDeducted);
    }

    [Fact]
    public async Task DismissReportAsync_UpdatesStatusToRejectedAndRecordsAuditLog()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new ReportService(dbContext, NullLogger<ReportService>.Instance);

        var reporterId = Guid.NewGuid();
        var reportedId = Guid.NewGuid();
        var staffId = Guid.NewGuid();

        dbContext.Users.AddRange(
            new User { Id = reporterId, Email = "r1@nexus.local", PasswordHash = "h", FirstName = "R", LastName = "One", Status = UserStatus.Active },
            new User { Id = reportedId, Email = "r2@nexus.local", PasswordHash = "h", FirstName = "R", LastName = "Two", Status = UserStatus.Active });
        await dbContext.SaveChangesAsync();

        var created = await service.CreateReportAsync(reporterId, new CreateReportRequest
        {
            ReportedUserId = reportedId,
            ViolationType = ReportViolationType.Spam,
            Description = "Suspicious message in chat."
        });

        var dismissed = await service.DismissReportAsync(
            created.ReportId,
            new DismissReportRequest { Reason = "Insufficient evidence of policy violation after reviewing logs." },
            staffId);

        Assert.Equal("Rejected", dismissed.Status);
        Assert.Equal(staffId, dismissed.ResolvedBy);
    }

    [Fact]
    public async Task ResolveReportAsync_WhenNoPenaltyOrScoreDeduction_DoesNotCreateUserPenaltyRecord()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new ReportService(dbContext, NullLogger<ReportService>.Instance);

        var reporterId = Guid.NewGuid();
        var reportedId = Guid.NewGuid();
        var staffId = Guid.NewGuid();

        dbContext.Users.AddRange(
            new User { Id = reporterId, Email = "r3@nexus.local", PasswordHash = "h", FirstName = "R", LastName = "Three", Status = UserStatus.Active },
            new User { Id = reportedId, Email = "r4@nexus.local", PasswordHash = "h", FirstName = "R", LastName = "Four", Status = UserStatus.Active });
        await dbContext.SaveChangesAsync();

        var created = await service.CreateReportAsync(reporterId, new CreateReportRequest
        {
            ReportedUserId = reportedId,
            ViolationType = ReportViolationType.Other,
            Description = "Requesting invoice assistance from seller."
        });

        var resolved = await service.ResolveReportAsync(
            created.ReportId,
            new ResolveReportRequest
            {
                PenaltyApplied = null,
                ScoreDeduction = 0,
                ResolutionNote = "Issue resolved amicably after seller sent invoice."
            },
            staffId);

        Assert.Equal("Resolved", resolved.Status);
        Assert.Null(resolved.PenaltyId);
        Assert.Null(resolved.PenaltyApplied);
        Assert.Empty(dbContext.UserPenalties);
    }
}
