namespace User.Application.Services.Admin;

using System;
using System.Threading;
using System.Threading.Tasks;
using User.Application.Common.Models;
using User.Domain.Enums;

public interface IAdminPenaltyService
{
    Task<PagedData<AdminPenaltyListItemDto>> GetPenaltiesAsync(
        int page = 1,
        int limit = 20,
        Guid? userId = null,
        TargetRole? targetRole = null,
        PenaltyStatus? status = null,
        CancellationToken cancellationToken = default);

    Task<AdminPenaltyDetailDto> GetPenaltyDetailAsync(
        Guid penaltyId,
        CancellationToken cancellationToken = default);

    Task<AdminPenaltyRevokedResponseDto> RevokePenaltyAsync(
        Guid penaltyId,
        RevokePenaltyDto dto,
        Guid adminId,
        CancellationToken cancellationToken = default);

    Task<UserActionPermissionCheckResponseDto> CheckUserActionPermissionAsync(
        Guid userId,
        string action,
        CancellationToken cancellationToken = default);
}
