namespace User.Infrastructure.Services.External;

using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using User.Application.Interfaces;

public sealed class WalletServiceClient : IWalletServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WalletServiceClient> _logger;

    public WalletServiceClient(HttpClient httpClient, ILogger<WalletServiceClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> FreezeWalletAsync(
        Guid userId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return false;
        }

        try
        {
            var payload = new
            {
                userId,
                reason = string.IsNullOrWhiteSpace(reason) ? "ACTIVE_DISPUTE_INVESTIGATION" : reason.Trim()
            };

            _logger.LogInformation("[WALLET CLIENT] Freezing wallet for User '{UserId}' due to '{Reason}'", userId, payload.reason);
            var response = await _httpClient.PostAsJsonAsync("api/v1/internal/wallets/freeze-purchase", payload, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("[WALLET CLIENT] Freeze wallet returned {StatusCode} for User '{UserId}': {Error}", (int)response.StatusCode, userId, errorBody);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[WALLET CLIENT] Error calling WalletService to freeze wallet for User '{UserId}'.", userId);
            return false;
        }
    }

    public async Task<bool> UnfreezeWalletAsync(
        Guid userId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return false;
        }

        try
        {
            var payload = new
            {
                userId,
                reason = string.IsNullOrWhiteSpace(reason) ? "DISPUTE_RESOLVED" : reason.Trim()
            };

            _logger.LogInformation("[WALLET CLIENT] Unfreezing wallet for User '{UserId}' due to '{Reason}'", userId, payload.reason);
            var response = await _httpClient.PostAsJsonAsync("api/v1/internal/wallets/unfreeze-purchase", payload, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("[WALLET CLIENT] Unfreeze wallet returned {StatusCode} for User '{UserId}': {Error}", (int)response.StatusCode, userId, errorBody);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[WALLET CLIENT] Error calling WalletService to unfreeze wallet for User '{UserId}'.", userId);
            return false;
        }
    }
}
