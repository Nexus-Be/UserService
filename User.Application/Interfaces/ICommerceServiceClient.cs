namespace User.Application.Interfaces;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Verifies order ownership and delivery state with CommerceService before auto-freezing dispute wallets.
/// </summary>
/// <summary>Result of checking an order against a report: only <see cref="Eligible"/> lets the report be saved.</summary>
public enum OrderReportCheck
{
    /// <summary>The reporter is the buyer, the reported user is a seller of the order and the goods were delivered.</summary>
    Eligible = 0,
    /// <summary>Commerce does not know the order or sub-order.</summary>
    NotFound = 1,
    /// <summary>The reporter is not the buyer or the reported user is not the seller.</summary>
    NotParticipant = 2,
    /// <summary>The order exists but is not delivered or completed yet.</summary>
    NotDelivered = 3,
    /// <summary>Commerce could not be reached or gave an unusable answer; the caller must not treat the order as verified.</summary>
    Unavailable = 4
}

public interface ICommerceServiceClient
{
    /// <summary>Checks the order and tells why it is not eligible, so a report is not saved on an unverified reference.</summary>
    Task<OrderReportCheck> CheckOrderForReportAsync(
        Guid orderOrSubOrderId,
        Guid reporterId,
        Guid reportedUserId,
        CancellationToken cancellationToken = default);

    Task<bool> VerifyDeliveredOrderForDisputeAsync(
        Guid orderOrSubOrderId,
        Guid reporterId,
        Guid reportedUserId,
        CancellationToken cancellationToken = default);
}
