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
    Task<InternalPenaltyRevokeResponse> RevokeBuyerRefusalPenaltyAsync(InternalPenaltyRevokeRequest request, CancellationToken cancellationToken = default);
    Task<AuctionDepositProfileResponse> GetAuctionDepositProfileAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<RecordAuctionViolationResponse> RecordAuctionViolationAsync(Guid userId, RecordAuctionViolationRequest request, CancellationToken cancellationToken = default);
    Task<RecordAuctionPaymentResponse> RecordAuctionPaymentAsync(Guid userId, RecordAuctionPaymentRequest request, CancellationToken cancellationToken = default);
    Task<InternalShopResponse?> GetShopAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<InternalContactResponse?> GetContactAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<InternalUserAddressResponse?> GetUserDefaultAddressAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<InternalUserAddressResponse?> GetUserAddressByIdAsync(Guid userId, Guid addressId, CancellationToken cancellationToken = default);
    Task<InternalUserDetailResponse?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<BatchSellerInfoResponse> GetBatchSellerInfoAsync(BatchSellerInfoRequest request, CancellationToken cancellationToken = default);
}
