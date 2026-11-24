namespace User.Infrastructure.Services.External;

using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using User.Application.Interfaces;

public sealed class FulfillmentServiceClient : IFulfillmentServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FulfillmentServiceClient> _logger;

    public FulfillmentServiceClient(HttpClient httpClient, ILogger<FulfillmentServiceClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<LocationCheck> ValidateLocationAsync(int provinceId, int districtId, string wardCode, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                "api/v1/internal/shipping/locations/validate",
                new { provinceId, districtId, wardCode },
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var ok = await response.Content.ReadFromJsonAsync<ValidateLocationBody>(cancellationToken: cancellationToken);
                return LocationCheck.Confirmed(ok?.ProvinceName, ok?.DistrictName, ok?.WardName);
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest && body.Contains("VAL_INVALID_LOCATION", StringComparison.Ordinal))
            {
                return LocationCheck.Rejected(ReadMessage(body));
            }

            _logger.LogWarning("[FULFILLMENT CLIENT] Location check answered {StatusCode}; the address is not saved until the GHN lists can be read.", (int)response.StatusCode);
            return LocationCheck.SourceUnavailable();
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "[FULFILLMENT CLIENT] Location check could not be completed.");
            return LocationCheck.SourceUnavailable();
        }
    }

    private sealed record ValidateLocationBody(string? ProvinceName, string? DistrictName, string? WardName);

    private static string? ReadMessage(string body)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("message", out var m) && m.ValueKind == System.Text.Json.JsonValueKind.String ? m.GetString() : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    public async Task<bool> CreateWarehouseAsync(
        Guid sellerId,
        Guid userId,
        string shopName,
        string contactName,
        string contactPhone,
        string pickupAddress,
        int provinceId,
        int districtId,
        string wardCode,
        CancellationToken cancellationToken = default)
    {
        var effectiveSellerId = userId != Guid.Empty ? userId : sellerId;
        var payload = new
        {
            seller_id = effectiveSellerId,
            user_id = effectiveSellerId,
            shop_name = shopName,
            contact_name = contactName,
            contact_phone = contactPhone,
            pickup_address = pickupAddress,
            province_id = provinceId,
            district_id = districtId,
            ward_code = wardCode
        };

        try
        {
            _logger.LogInformation("[FULFILLMENT CLIENT] Creating warehouse for Seller '{SellerId}' (User: '{UserId}'), Shop: '{ShopName}' at public route /api/v1/warehouses/initialize",
                sellerId, effectiveSellerId, shopName);
            var response = await _httpClient.PostAsJsonAsync("api/v1/warehouses/initialize", payload, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("[FULFILLMENT CLIENT] Route 'api/v1/warehouses/initialize' returned 404 for Seller '{SellerId}'. Retrying via legacy route 'api/v1/internal/warehouses'...",
                    effectiveSellerId);
                response.Dispose();
                response = await _httpClient.PostAsJsonAsync("api/v1/internal/warehouses", payload, cancellationToken);
            }

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[FULFILLMENT CLIENT] Successfully created warehouse for Seller '{SellerId}'. StatusCode: {StatusCode}", effectiveSellerId, (int)response.StatusCode);
                return true;
            }

            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("[FULFILLMENT CLIENT] Warehouse creation rejected by FulfillmentService for Seller '{SellerId}'. StatusCode: {StatusCode}, Error: {Error}", effectiveSellerId, (int)response.StatusCode, errorBody);
            return false;
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "[FULFILLMENT CLIENT] Warehouse creation timed out (>5s) for Seller '{SellerId}'.", effectiveSellerId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FULFILLMENT CLIENT] Network or unexpected failure while calling FulfillmentService for Seller '{SellerId}'.", effectiveSellerId);
            return false;
        }
    }

    public async Task<bool> UpdateWarehouseAsync(
        Guid sellerId,
        string shopName,
        string contactPhone,
        string pickupAddress,
        int provinceId,
        int districtId,
        string wardCode,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            shop_name = shopName,
            contact_phone = contactPhone,
            pickup_address = pickupAddress,
            province_id = provinceId,
            district_id = districtId,
            ward_code = wardCode
        };

        try
        {
            _logger.LogInformation("[FULFILLMENT CLIENT] Updating warehouse for Seller '{SellerId}'", sellerId);
            var response = await _httpClient.PutAsJsonAsync($"api/v1/internal/warehouses/seller/{sellerId}", payload, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[FULFILLMENT CLIENT] Successfully updated warehouse for Seller '{SellerId}'.", sellerId);
                return true;
            }

            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("[FULFILLMENT CLIENT] Warehouse update failed for Seller '{SellerId}'. StatusCode: {StatusCode}, Error: {Error}", sellerId, (int)response.StatusCode, errorBody);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FULFILLMENT CLIENT] Unexpected error updating warehouse for Seller '{SellerId}'.", sellerId);
            return false;
        }
    }

    public async Task<bool> DeleteWarehouseBySellerIdAsync(
        Guid sellerId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("[FULFILLMENT CLIENT] Compensating action: Deleting orphan warehouse for Seller '{SellerId}'", sellerId);
            var response = await _httpClient.DeleteAsync($"api/v1/internal/warehouses/seller/{sellerId}", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[FULFILLMENT CLIENT] Failed to delete warehouse for Seller '{SellerId}' during compensation.", sellerId);
            return false;
        }
    }
}
