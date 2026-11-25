namespace User.Application.Interfaces;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>What the GHN lists (served by Fulfillment) said about a province, district and ward chain.</summary>
public enum LocationCheckOutcome
{
    /// <summary>The three codes are in the GHN lists and belong together.</summary>
    Confirmed,
    /// <summary>The GHN lists were read and the chain is not valid.</summary>
    Rejected,
    /// <summary>No trusted GHN source answered, so nothing can be said about the chain.</summary>
    SourceUnavailable,
}

public sealed record LocationCheck(
    LocationCheckOutcome Outcome,
    string? ProvinceName = null,
    string? DistrictName = null,
    string? WardName = null,
    string? Message = null)
{
    public static LocationCheck Confirmed(string? provinceName = null, string? districtName = null, string? wardName = null) =>
        new(LocationCheckOutcome.Confirmed, provinceName, districtName, wardName);

    public static LocationCheck Rejected(string? message = null) => new(LocationCheckOutcome.Rejected, Message: message);

    public static LocationCheck SourceUnavailable() => new(LocationCheckOutcome.SourceUnavailable);
}

public interface IFulfillmentServiceClient
{
    /// <summary>Asks Fulfillment whether the province, district and ward belong together in the GHN lists.</summary>
    Task<LocationCheck> ValidateLocationAsync(int provinceId, int districtId, string wardCode, CancellationToken cancellationToken = default);

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

