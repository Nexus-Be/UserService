using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace User.Application.Services.Address;

public interface IAddressService
{
    Task<IReadOnlyList<UserAddressDto>> GetAddressesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserAddressDto?> GetDefaultAddressAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserAddressDto> CreateAddressAsync(Guid userId, CreateAddressRequest request, CancellationToken cancellationToken = default);
    Task<UserAddressDto> UpdateAddressAsync(Guid userId, Guid addressId, UpdateAddressRequest request, CancellationToken cancellationToken = default);
    Task<UserAddressDto> SetDefaultAddressAsync(Guid userId, Guid addressId, CancellationToken cancellationToken = default);
    Task DeleteAddressAsync(Guid userId, Guid addressId, CancellationToken cancellationToken = default);
}
