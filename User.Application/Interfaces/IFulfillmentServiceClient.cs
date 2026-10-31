namespace User.Application.Interfaces;

using System;
using System.Threading;
using System.Threading.Tasks;

public interface IFulfillmentServiceClient
{
    Task<bool> CreateWarehouseAsync(
        Guid sellerId,
        Guid userId,
        string shopName,
        string contactName,
        string contactPhone,
        string pickupAddress,
        int provinceId,
        int districtId,
        string wardCode,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateWarehouseAsync(
        Guid sellerId,
        string shopName,
        string contactPhone,
        string pickupAddress,
        int provinceId,
        int districtId,
        string wardCode,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteWarehouseBySellerIdAsync(
        Guid sellerId,
        CancellationToken cancellationToken = default);
}

