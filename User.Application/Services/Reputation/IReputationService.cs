namespace User.Application.Services.Reputation;

using System;
using System.Threading;
using System.Threading.Tasks;
using User.Application.Common.Models;
using User.Domain.Enums;

public interface IReputationService
{
    Task<MyReputationResponse> GetMyReputationAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<PublicReputationResponse> GetUserReputationSummaryAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<PagedData<ReputationHistoryItemDto>> GetUserReputationHistoryAsync(
        Guid targetUserId,
        Guid requesterId,
        bool isStaffOrAdmin,
        int page = 1,
        int pageSize = 20,
        TargetRole? targetRole = null,
        CancellationToken cancellationToken = default);
    Task<ReputationPolicyResponse> GetReputationPolicyAsync(CancellationToken cancellationToken = default);
    Task<PagedData<MyPenaltyItemDto>> GetMyPenaltiesAsync(
        Guid userId,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);
    Task<AdjustReputationResponse> AdjustReputationAsync(
        Guid targetUserId,
        AdjustReputationRequest request,
        Guid adminId,
        CancellationToken cancellationToken = default);
}
