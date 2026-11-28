namespace User.Application.Interfaces;

using System;
using System.Threading;
using System.Threading.Tasks;

public interface IWalletServiceClient
{
    Task<bool> FreezeWalletAsync(
        Guid userId,
        string reason,
        CancellationToken cancellationToken = default);

    Task<bool> UnfreezeWalletAsync(
        Guid userId,
        string reason,
        CancellationToken cancellationToken = default);
}
