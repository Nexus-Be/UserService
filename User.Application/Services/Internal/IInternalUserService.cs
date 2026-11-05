namespace User.Application.Services.Internal;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public interface IInternalUserService
{
    Task<CheckReputationResponse> CheckReputationAsync(CheckReputationRequest request, CancellationToken cancellationToken = default);
    Task<VerifyBidEligibilityResponse> VerifyBidEligibilityAsync(VerifyBidEligibilityRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserPublicInfoDto>> GetBatchPublicInfoAsync(BatchPublicInfoRequest request, CancellationToken cancellationToken = default);
}
