namespace User.Application.Common;

using System.Threading;
using System.Threading.Tasks;
using User.Application.Interfaces;
using User.Domain.Exceptions;

/// <summary>
/// An address is saved only when the GHN lists confirm the whole province, district and ward chain. With no trusted GHN source the answer is
/// 503 LOCATION_SOURCE_UNAVAILABLE and nothing is saved; a chain the lists do not accept is 400 VAL_INVALID_LOCATION.
/// </summary>
public static class LocationVerification
{
    public static async Task<LocationCheck> EnsureConfirmedAsync(
        IFulfillmentServiceClient fulfillment,
        int provinceId,
        int districtId,
        string wardCode,
        CancellationToken cancellationToken = default)
    {
        var check = await fulfillment.ValidateLocationAsync(provinceId, districtId, wardCode, cancellationToken);
        return check.Outcome switch
        {
            LocationCheckOutcome.Confirmed => check,
            LocationCheckOutcome.Rejected => throw new BusinessRuleValidationException("VAL_INVALID_LOCATION", check.Message ?? "The province, district and ward do not match.", 400),
            _ => throw new BusinessRuleValidationException("LOCATION_SOURCE_UNAVAILABLE", "Shipping locations are temporarily unavailable, so the address cannot be saved yet.", 503),
        };
    }
}
