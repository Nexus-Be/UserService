namespace User.Application.Services.Internal;

using System;
using System.Threading;
using System.Threading.Tasks;

public interface IInternalUserService
{
    Task<CheckReputationResponse> CheckReputationAsync(CheckReputationRequest request, CancellationToken cancellationToken = default);
    Task<VerifyBidEligibilityResponse> VerifyBidEligibilityAsync(VerifyBidEligibilityRequest request, CancellationToken cancellationToken = default);
    Task<BatchPublicInfoResponse> GetBatchPublicInfoAsync(BatchPublicInfoRequest request, CancellationToken cancellationToken = default);
    Task<InternalPenaltyResponse> ApplyInternalPenaltyAsync(InternalPenaltyRequest request, CancellationToken cancellationToken = default);
    Task<InternalReputationAdjustResponse> ApplyInternalReputationAdjustAsync(InternalReputationAdjustRequest request, CancellationToken cancellationToken = default);
    Task<AuctionDepositProfileResponse> GetAuctionDepositProfileAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<RecordAuctionViolationResponse> RecordAuctionViolationAsync(Guid userId, RecordAuctionViolationRequest request, CancellationToken cancellationToken = default);
}
