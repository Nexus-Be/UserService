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
}
