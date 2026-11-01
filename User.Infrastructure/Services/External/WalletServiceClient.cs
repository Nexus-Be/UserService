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

    public async Task<bool> CreateWalletAsync(
        Guid userId,
        string currency = "VND",
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            userId = userId,
            currency = string.IsNullOrWhiteSpace(currency) ? "VND" : currency.Trim()
        };

        try
        {
            _logger.LogInformation("[WALLET CLIENT] Initiating wallet creation for User '{UserId}' with currency '{Currency}' at 'api/v1/wallets'", userId, payload.currency);
            var response = await _httpClient.PostAsJsonAsync("api/v1/wallets", payload, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[WALLET CLIENT] Successfully initialized wallet for User '{UserId}'. StatusCode: {StatusCode}", userId, (int)response.StatusCode);
                return true;
            }

            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("[WALLET CLIENT] Wallet creation rejected or failed for User '{UserId}'. StatusCode: {StatusCode}, Error: {Error}", userId, (int)response.StatusCode, errorBody);
            return false;
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "[WALLET CLIENT] Wallet creation timed out (>5s) for User '{UserId}'.", userId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WALLET CLIENT] Network or unexpected failure while calling WalletService for User '{UserId}'.", userId);
            return false;
        }
    }
}
