namespace User.Application.Services.Seller;

using System;
using System.Threading;
using System.Threading.Tasks;

public interface ISellerService
{
    Task<SellerRegistrationResponseDto> RegisterSellerAsync(
        Guid userId,
        CreateSellerRegistrationDto dto,
        CancellationToken cancellationToken = default);

    Task<PagedResultDto<AdminSellerRegistrationItemDto>> GetSellerRegistrationsAsync(
        int page,
        int limit,
        string? status,
        CancellationToken cancellationToken = default);

    Task<ApproveSellerRegistrationResponseDto> ApproveSellerRegistrationAsync(
        Guid registrationId,
        Guid? reviewerId,
        ApproveSellerRegistrationDto? dto,
        CancellationToken cancellationToken = default);

    Task<RejectSellerRegistrationResponseDto> RejectSellerRegistrationAsync(
        Guid registrationId,
        Guid? reviewerId,
        RejectSellerRegistrationDto dto,
        CancellationToken cancellationToken = default);

    Task<SellerOwnProfileResponseDto> GetSellerOwnProfileAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<SellerOwnProfileUpdatedResponseDto> UpdateSellerOwnProfileAsync(
        Guid userId,
        UpdateSellerOwnProfileDto dto,
        CancellationToken cancellationToken = default);
}
