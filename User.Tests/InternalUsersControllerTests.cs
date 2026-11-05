namespace User.Tests;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using User.Api.Controllers.Internal;
using User.Application.Common.Models;
using User.Application.Services.Admin;
using User.Application.Services.Internal;
using User.Domain.Enums;
using Xunit;

public sealed class InternalUsersControllerTests
{
    private sealed class FakeInternalUserService : IInternalUserService
    {
        public CheckReputationResponse CheckReputationResult { get; set; } = default!;
        public VerifyBidEligibilityResponse VerifyBidEligibilityResult { get; set; } = default!;
        public IReadOnlyList<UserPublicInfoDto> BatchPublicInfoResult { get; set; } = default!;

        public Task<CheckReputationResponse> CheckReputationAsync(CheckReputationRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CheckReputationResult);
        }

        public Task<VerifyBidEligibilityResponse> VerifyBidEligibilityAsync(VerifyBidEligibilityRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(VerifyBidEligibilityResult);
        }

        public Task<IReadOnlyList<UserPublicInfoDto>> GetBatchPublicInfoAsync(BatchPublicInfoRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(BatchPublicInfoResult);
        }
    }

    private sealed class FakePenaltyService : IAdminPenaltyService
    {
        public Task<UserActionPermissionCheckResponseDto> CheckUserActionPermissionAsync(Guid userId, string action, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<AdminPenaltyDetailDto> GetPenaltyDetailAsync(Guid penaltyId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<PagedData<AdminPenaltyListItemDto>> GetPenaltiesAsync(int page = 1, int limit = 20, Guid? userId = null, TargetRole? targetRole = null, PenaltyStatus? status = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<AdminPenaltyRevokedResponseDto> RevokePenaltyAsync(Guid penaltyId, RevokePenaltyDto dto, Guid adminId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    [Fact]
    public async Task CheckReputation_ReturnsOkResult_WithExpectedPayload()
    {
        var fakeInternalService = new FakeInternalUserService
        {
            CheckReputationResult = new CheckReputationResponse(
                UserId: Guid.NewGuid().ToString(),
                Allowed: true,
                IsApprovedSeller: true,
                UserStatus: "Active",
                FailureReason: null)
        };

        var controller = new InternalUsersController(
            new FakePenaltyService(),
            fakeInternalService,
            NullLogger<InternalUsersController>.Instance);

        var request = new CheckReputationRequest("SELL", Guid.NewGuid());
        var actionResult = await controller.CheckReputation(request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(200, okResult.StatusCode);
        Assert.Same(fakeInternalService.CheckReputationResult, okResult.Value);
    }

    [Fact]
    public async Task VerifyBidEligibility_ReturnsOkResult_WithExpectedPayload()
    {
        var fakeInternalService = new FakeInternalUserService
        {
            VerifyBidEligibilityResult = new VerifyBidEligibilityResponse(
                UserId: Guid.NewGuid().ToString(),
                Allowed: true,
                FailureReason: null)
        };

        var controller = new InternalUsersController(
            new FakePenaltyService(),
            fakeInternalService,
            NullLogger<InternalUsersController>.Instance);

        var request = new VerifyBidEligibilityRequest(Guid.NewGuid());
        var actionResult = await controller.VerifyBidEligibility(request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(200, okResult.StatusCode);
        Assert.Same(fakeInternalService.VerifyBidEligibilityResult, okResult.Value);
    }

    [Fact]
    public async Task GetBatchPublicInfo_ReturnsOkResult_WithExpectedList()
    {
        var fakeInternalService = new FakeInternalUserService
        {
            BatchPublicInfoResult = new List<UserPublicInfoDto>
            {
                new(UserId: Guid.NewGuid().ToString(), MaskedName: "j***e", IsDeleted: false)
            }
        };

        var controller = new InternalUsersController(
            new FakePenaltyService(),
            fakeInternalService,
            NullLogger<InternalUsersController>.Instance);

        var request = new BatchPublicInfoRequest(new List<Guid> { Guid.NewGuid() });
        var actionResult = await controller.GetBatchPublicInfo(request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(200, okResult.StatusCode);
        Assert.Same(fakeInternalService.BatchPublicInfoResult, okResult.Value);
    }
}
