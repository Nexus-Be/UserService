namespace User.Tests;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using User.Api.Controllers;
using User.Api.Controllers.Internal;
using User.Application.Services.Admin;
using User.Application.Services.AdminConfig;
using User.Application.Services.Internal;
using User.Application.Services.Profile;
using User.Application.Services.Report;
using User.Application.Services.Reputation;
using User.Application.Services.Review;
using User.Domain.Entities;
using User.Domain.Enums;
using User.Infrastructure.Persistence;
using Xunit;

public sealed class ReputationAndGovernanceControllersTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"rep_controllers_test_db_{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    private static void AttachUserHeader(ControllerBase controller, Guid userId, string roles = "buyer")
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["x-user-id"] = userId.ToString();
        httpContext.Request.Headers["x-user-roles"] = roles;
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    [Fact]
    public async Task MeController_GetMyReputation_ReturnsDualScores()
    {
        using var dbContext = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        dbContext.Users.Add(new User
        {
            Id = userId,
            Email = "me@nexus.local",
            PasswordHash = "hash",
            FirstName = "Me",
            LastName = "User",
            Status = UserStatus.Active
        });
        dbContext.ReputationScores.Add(new ReputationScore
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CurrentScore = 75,
            TrustLevel = TrustLevel.Good,
            SellerScore = 50,
            SellerTrustLevel = TrustLevel.Standard
        });
        await dbContext.SaveChangesAsync();

        var profileService = new ProfileService(dbContext, NullLogger<ProfileService>.Instance);
        var repService = new ReputationService(dbContext, NullLogger<ReputationService>.Instance);
        var reportService = new ReportService(dbContext, NullLogger<ReportService>.Instance);
        var controller = new MeController(profileService, NullLogger<MeController>.Instance, repService, reportService);
        AttachUserHeader(controller, userId);

        var actionResult = await controller.GetMyReputation(CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(actionResult);
        var dto = Assert.IsType<MyReputationResponse>(ok.Value);
        Assert.Equal(75, dto.CurrentScore);
        Assert.Equal(50, dto.SellerScore);
    }

    [Fact]
    public async Task ReviewsAndAdminReviewsControllers_CreateAndHideReview_Returns201And200()
    {
        using var dbContext = CreateInMemoryDbContext();
        var buyerId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var staffId = Guid.NewGuid();

        dbContext.Users.AddRange(
            new User { Id = buyerId, Email = "b@nexus.local", PasswordHash = "h", FirstName = "B", LastName = "U", Status = UserStatus.Active },
            new User { Id = sellerId, Email = "s@nexus.local", PasswordHash = "h", FirstName = "S", LastName = "E", Status = UserStatus.Active });
        dbContext.SellerProfiles.Add(new SellerProfile
        {
            Id = Guid.NewGuid(),
            UserId = sellerId,
            ShopName = "Test Shop",
            PickupAddress = "123 St",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "W1",
            Hotline = "0900000000",
            Status = SellerProfileStatus.Approved
        });
        dbContext.ReputationScores.Add(new ReputationScore { Id = Guid.NewGuid(), UserId = sellerId, CurrentScore = 50, SellerScore = 50 });
        await dbContext.SaveChangesAsync();

        var reviewService = new ReviewService(dbContext, NullLogger<ReviewService>.Instance);
        var reviewsController = new ReviewsController(reviewService, NullLogger<ReviewsController>.Instance);
        AttachUserHeader(reviewsController, buyerId, "buyer");

        var createAction = await reviewsController.CreateReview(new CreateReviewRequest
        {
            RevieweeId = sellerId,
            ReviewerRole = ReviewerRole.Buyer,
            ReferenceType = ReferenceType.Order,
            ReferenceId = "ORD-CTRL-1",
            Rating = 1,
            Comment = "Unfair 1 star review."
        }, CancellationToken.None);

        var createdObj = Assert.IsType<ObjectResult>(createAction);
        Assert.Equal(StatusCodes.Status201Created, createdObj.StatusCode);
        var createdDto = Assert.IsType<CreateReviewResponse>(createdObj.Value);

        var adminReviewsController = new AdminReviewsController(reviewService, NullLogger<AdminReviewsController>.Instance);
        AttachUserHeader(adminReviewsController, staffId, "staff");

        var hideAction = await adminReviewsController.HideReview(
            createdDto.ReviewId,
            new HideReviewRequest { Reason = "Verified unfair review during dispute moderation." },
            CancellationToken.None);

        var hideOk = Assert.IsType<OkObjectResult>(hideAction);
        var hideDto = Assert.IsType<HideReviewResponse>(hideOk.Value);
        Assert.True(hideDto.IsDisabled);
        Assert.Equal(50, hideDto.RevieweeNewScore);
    }

    [Fact]
    public async Task InternalUsersController_AllFiveEndpoints_Return200Ok()
    {
        using var dbContext = CreateInMemoryDbContext();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var userId = Guid.NewGuid();

        dbContext.Users.Add(new User
        {
            Id = userId,
            Email = "internal@nexus.local",
            PasswordHash = "h",
            FirstName = "Internal",
            LastName = "User",
            Status = UserStatus.Active
        });
        dbContext.ReputationScores.Add(new ReputationScore
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CurrentScore = 60,
            SellerScore = 55
        });
        await dbContext.SaveChangesAsync();

        var penaltyService = new AdminPenaltyService(dbContext, NullLogger<AdminPenaltyService>.Instance);
        var internalService = new InternalUserService(dbContext, cache, NullLogger<InternalUserService>.Instance);
        var controller = new InternalUsersController(penaltyService, NullLogger<InternalUsersController>.Instance, internalService);

        var checkRep = await controller.CheckReputation(new CheckReputationRequest { UserId = userId, RequiredAction = "CREATE_AUCTION" }, CancellationToken.None);
        Assert.IsType<OkObjectResult>(checkRep.Result);

        var verifyBid = await controller.VerifyBidEligibility(new VerifyBidEligibilityRequest { UserId = userId, AuctionId = Guid.NewGuid() }, CancellationToken.None);
        Assert.IsType<OkObjectResult>(verifyBid.Result);

        var batchInfo = await controller.GetBatchPublicInfo(new BatchPublicInfoRequest { UserIds = new List<Guid> { userId } }, CancellationToken.None);
        Assert.IsType<OkObjectResult>(batchInfo.Result);

        var penalty = await controller.ApplyInternalPenalty(new InternalPenaltyRequest
        {
            UserId = userId,
            TargetRole = TargetRole.Buyer,
            ViolationType = PenaltyViolationType.PaymentTimeout,
            ReferenceType = ReferenceType.Auction,
            ReferenceId = "AUC-INT-1",
            Reason = "Payment timeout test"
        }, CancellationToken.None);
        Assert.IsType<OkObjectResult>(penalty.Result);

        var adjust = await controller.ApplyInternalReputationAdjust(new InternalReputationAdjustRequest
        {
            UserId = userId,
            TargetRole = TargetRole.Seller,
            ScoreDelta = 1,
            Reason = ReputationChangeReason.OrderCompleted,
            ReferenceType = ReferenceType.Order,
            ReferenceId = "ORD-INT-1"
        }, CancellationToken.None);
        Assert.IsType<OkObjectResult>(adjust.Result);
    }
}
