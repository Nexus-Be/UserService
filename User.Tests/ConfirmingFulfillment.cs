namespace User.Tests;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using User.Application.Interfaces;

/// <summary>A stand-in for Fulfillment in tests: the GHN lists confirm every chain, unless it is told otherwise. It records what it was asked.</summary>
public sealed class ConfirmingFulfillment : IFulfillmentServiceClient
{
    public LocationCheck Answer { get; set; } = LocationCheck.Confirmed("Ho Chi Minh", "Quan 1", "Phuong Ben Nghe");
    public List<(int Province, int District, string Ward)> Asked { get; } = new();

    public Task<LocationCheck> ValidateLocationAsync(int provinceId, int districtId, string wardCode, CancellationToken cancellationToken = default)
    {
        Asked.Add((provinceId, districtId, wardCode));
        return Task.FromResult(Answer);
    }

    public Task<bool> CreateWarehouseAsync(Guid sellerId, Guid userId, string shopName, string contactName, string contactPhone, string pickupAddress, int provinceId, int districtId, string wardCode, CancellationToken cancellationToken = default) => Task.FromResult(true);

    public Task<bool> UpdateWarehouseAsync(Guid sellerId, string shopName, string contactPhone, string pickupAddress, int provinceId, int districtId, string wardCode, CancellationToken cancellationToken = default) => Task.FromResult(true);

    public Task<bool> DeleteWarehouseBySellerIdAsync(Guid sellerId, CancellationToken cancellationToken = default) => Task.FromResult(true);
}
