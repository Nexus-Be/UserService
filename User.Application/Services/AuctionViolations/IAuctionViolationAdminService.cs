namespace User.Application.Services.AuctionViolations;

using System;
using System.Threading;
using System.Threading.Tasks;

public interface IAuctionViolationAdminService
{
    Task<AdminAuctionViolationsResponse> GetAuctionViolationsAsync(
        Guid userId,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<AdjustAuctionViolationResponse> AdjustAuctionViolationAsync(
        Guid userId,
        AdjustAuctionViolationRequest request,
        Guid adminId,
        string? ipAddress = null,
        string? userAgent = null,
        string? traceId = null,
        CancellationToken cancellationToken = default);
}
