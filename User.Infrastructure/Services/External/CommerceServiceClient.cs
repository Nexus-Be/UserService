namespace User.Infrastructure.Services.External;

using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using User.Application.Interfaces;

public sealed class CommerceServiceClient : ICommerceServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CommerceServiceClient> _logger;

    public CommerceServiceClient(HttpClient httpClient, ILogger<CommerceServiceClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> VerifyDeliveredOrderForDisputeAsync(
        Guid orderOrSubOrderId,
        Guid reporterId,
        Guid reportedUserId,
        CancellationToken cancellationToken = default)
    {
        if (orderOrSubOrderId == Guid.Empty || reporterId == Guid.Empty || reportedUserId == Guid.Empty)
        {
            return false;
        }

        try
        {
            using var subOrderDoc = await TryFetchJsonAsync($"api/v1/sub-orders/{orderOrSubOrderId}", cancellationToken);
            if (subOrderDoc != null)
            {
                var payload = UnwrapData(subOrderDoc.RootElement);
                if (TryReadGuid(payload, "buyerId", out var buyerId)
                    && TryReadGuid(payload, "sellerId", out var sellerId)
                    && TryReadString(payload, "status", out var status))
                {
                    return IsValidDeliveredDispute(buyerId, sellerId, status, reporterId, reportedUserId);
                }
            }

            using var orderDoc = await TryFetchJsonAsync($"api/v1/orders/{orderOrSubOrderId}", cancellationToken);
            if (orderDoc != null)
            {
                var orderPayload = UnwrapData(orderDoc.RootElement);
                if (TryReadGuid(orderPayload, "buyerId", out var orderBuyerId)
                    && orderBuyerId == reporterId
                    && TryGetPropertyIgnoreCase(orderPayload, "subOrders", out var subOrdersEl)
                    && subOrdersEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var subEl in subOrdersEl.EnumerateArray())
                    {
                        if (TryReadGuid(subEl, "sellerId", out var sellerId)
                            && TryReadString(subEl, "status", out var status)
                            && IsValidDeliveredDispute(orderBuyerId, sellerId, status, reporterId, reportedUserId))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[COMMERCE CLIENT] Failed to verify order '{OrderId}' for dispute.", orderOrSubOrderId);
            return false;
        }
    }

    private async Task<JsonDocument?> TryFetchJsonAsync(string relativeUrl, CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync(relativeUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return JsonDocument.Parse(raw);
    }

    private static JsonElement UnwrapData(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object
            && TryGetPropertyIgnoreCase(root, "data", out var dataEl)
            && dataEl.ValueKind == JsonValueKind.Object)
        {
            return dataEl;
        }

        return root;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement obj, string name, out JsonElement value)
    {
        if (obj.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in obj.EnumerateObject())
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = prop.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static bool TryReadGuid(JsonElement obj, string name, out Guid guid)
    {
        if (TryGetPropertyIgnoreCase(obj, name, out var el)
            && el.ValueKind == JsonValueKind.String
            && Guid.TryParse(el.GetString(), out guid))
        {
            return true;
        }

        guid = Guid.Empty;
        return false;
    }

    private static bool TryReadString(JsonElement obj, string name, out string? value)
    {
        if (TryGetPropertyIgnoreCase(obj, name, out var el) && el.ValueKind == JsonValueKind.String)
        {
            value = el.GetString();
            return true;
        }

        value = null;
        return false;
    }

    private static bool IsValidDeliveredDispute(
        Guid buyerId,
        Guid sellerId,
        string? status,
        Guid reporterId,
        Guid reportedUserId)
    {
        if (buyerId != reporterId || sellerId != reportedUserId || string.IsNullOrWhiteSpace(status))
        {
            return false;
        }

        var normalized = status.Trim().ToUpperInvariant();
        return normalized is "DELIVERED" or "COMPLETED";
    }
}
