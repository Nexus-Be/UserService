namespace User.Application.Interfaces;

using System;
using System.Threading;
using System.Threading.Tasks;

public interface IWalletServiceClient
{
    Task<bool> CreateWalletAsync(
        Guid userId,
        string currency = "VND",
        CancellationToken cancellationToken = default);
}
