namespace User.Infrastructure.Services.External;

using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using User.Application.Interfaces;

public sealed class CommerceServiceClient : ICommerceServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CommerceServiceClient> _logger;
    private readonly string? _internalServiceKey;

    public CommerceServiceClient(HttpClient httpClient, ILogger<CommerceServiceClient> logger, string? internalServiceKey = null)
    {
        _internalServiceKey = internalServiceKey;
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DisputeRefundResult> RequestDisputeRefundAsync(
        Guid subOrderId,
        Guid reportId,
        string? note,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"api/v1/sub-orders/{subOrderId}/dispute-refund")
            {
                Content = JsonContent.Create(new { reportId, note })
            };
            if (!string.IsNullOrWhiteSpace(_internalServiceKey))
            {
                request.Headers.TryAddWithoutValidation("X-Internal-Service-Key", _internalServiceKey);
            }

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[COMMERCE CLIENT] Dispute refund for sub-order '{SubOrderId}' answered {Status}: {Body}", subOrderId, (int)response.StatusCode, body);
                return new DisputeRefundResult(DisputeRefundOutcome.Failed, 0m, $"Commerce answered HTTP {(int)response.StatusCode}.");
            }

            using var doc = JsonDocument.Parse(body);
            var payload = UnwrapData(doc.RootElement);
            TryReadString(payload, "status", out var status);
            var amount = TryGetPropertyIgnoreCase(payload, "amount", out var amountEl) && amountEl.TryGetDecimal(out var parsed) ? parsed : 0m;
            TryReadString(payload, "message", out var message);
            var outcome = status switch
            {
                "Refunded" => DisputeRefundOutcome.Refunded,
                "AlreadyRefunded" => DisputeRefundOutcome.AlreadyRefunded,
                "ManualProcessingRequired" => DisputeRefundOutcome.ManualProcessingRequired,
                _ => DisputeRefundOutcome.Failed
            };
            return new DisputeRefundResult(outcome, amount, message);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "[COMMERCE CLIENT] Could not request the dispute refund for sub-order '{SubOrderId}'.", subOrderId);
            return new DisputeRefundResult(DisputeRefundOutcome.Failed, 0m, "Commerce could not be reached.");
        }
    }

    public async Task<OrderReportCheck> CheckOrderForReportAsync(
        Guid orderOrSubOrderId,
        Guid reporterId,
        Guid reportedUserId,
        CancellationToken cancellationToken = default)
    {
        if (orderOrSubOrderId == Guid.Empty || reporterId == Guid.Empty || reportedUserId == Guid.Empty)
        {
            return OrderReportCheck.NotFound;
        }

        try
        {
            var (subStatus, subDoc) = await FetchAsync($"api/v1/sub-orders/{orderOrSubOrderId}", cancellationToken);
            using (subDoc)
            {
                if (subStatus == HttpStatusCode.OK && subDoc != null)
                {
                    var payload = UnwrapData(subDoc.RootElement);
                    if (!TryReadGuid(payload, "buyerId", out var buyerId)
                        || !TryReadGuid(payload, "sellerId", out var sellerId)
                        || !TryReadString(payload, "status", out var status))
                    {
                        return OrderReportCheck.Unavailable;
                    }

                    return Classify(buyerId, sellerId, status, reporterId, reportedUserId);
                }

                if (subStatus != HttpStatusCode.NotFound)
                {
                    return OrderReportCheck.Unavailable;
                }
            }

            var (orderStatus, orderDoc) = await FetchAsync($"api/v1/orders/{orderOrSubOrderId}", cancellationToken);
            using (orderDoc)
            {
                if (orderStatus == HttpStatusCode.NotFound)
                {
                    return OrderReportCheck.NotFound;
                }

                if (orderStatus != HttpStatusCode.OK || orderDoc == null)
                {
                    return OrderReportCheck.Unavailable;
                }

                var orderPayload = UnwrapData(orderDoc.RootElement);
                if (!TryReadGuid(orderPayload, "buyerId", out var orderBuyerId)
                    || !TryGetPropertyIgnoreCase(orderPayload, "subOrders", out var subOrdersEl)
                    || subOrdersEl.ValueKind != JsonValueKind.Array)
                {
                    return OrderReportCheck.Unavailable;
                }

                var best = OrderReportCheck.NotParticipant;
                foreach (var subEl in subOrdersEl.EnumerateArray())
                {
                    if (!TryReadGuid(subEl, "sellerId", out var sellerId) || !TryReadString(subEl, "status", out var status))
                    {
                        continue;
                    }

                    var result = Classify(orderBuyerId, sellerId, status, reporterId, reportedUserId);
                    if (result == OrderReportCheck.Eligible)
                    {
                        return result;
                    }

                    // Keep the most specific reason: a matching seller that is not delivered beats "not a participant".
                    if (result == OrderReportCheck.NotDelivered)
                    {
                        best = result;
                    }
                }

                return best;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "[COMMERCE CLIENT] Could not check order '{OrderId}' for a report.", orderOrSubOrderId);
            return OrderReportCheck.Unavailable;
        }
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

    private async Task<(HttpStatusCode Status, JsonDocument? Document)> FetchAsync(string relativeUrl, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(relativeUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return (response.StatusCode, null);
        }

        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(raw)
            ? (HttpStatusCode.NoContent, null)
            : (HttpStatusCode.OK, JsonDocument.Parse(raw));
    }

    private static OrderReportCheck Classify(Guid buyerId, Guid sellerId, string? status, Guid reporterId, Guid reportedUserId)
    {
        if (buyerId != reporterId || sellerId != reportedUserId)
        {
            return OrderReportCheck.NotParticipant;
        }

        return IsValidDeliveredDispute(buyerId, sellerId, status, reporterId, reportedUserId)
            ? OrderReportCheck.Eligible
            : OrderReportCheck.NotDelivered;
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
