namespace User.Application.Interfaces;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Verifies order ownership and delivery state with CommerceService before auto-freezing dispute wallets.
/// </summary>
public interface ICommerceServiceClient
{
    Task<bool> VerifyDeliveredOrderForDisputeAsync(
        Guid orderOrSubOrderId,
        Guid reporterId,
        Guid reportedUserId,
        CancellationToken cancellationToken = default);
}
