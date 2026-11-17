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

/// <summary>Calls CommerceService's internal review-eligibility endpoint with the internal service key.</summary>
public sealed class CommerceOrderClient : ICommerceOrderClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ILogger<CommerceOrderClient> _logger;

    public CommerceOrderClient(HttpClient httpClient, ILogger<CommerceOrderClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<OrderReviewReference?> GetReviewReferenceAsync(
        string referenceType, Guid referenceId, CancellationToken cancellationToken = default)
    {
        var url = $"api/v1/internal/review-eligibility?referenceType={Uri.EscapeDataString(referenceType)}&referenceId={referenceId}";
        try
        {
            using var response = await _httpClient.GetAsync(url, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            if (!response.IsSuccessStatusCode)
            {
                // 401/503 mean the key is wrong or not configured on either side; 5xx means Commerce is failing. None of them is "no such order".
                _logger.LogError(
                    "[COMMERCE CLIENT] Review eligibility for {ReferenceType} '{ReferenceId}' failed with HTTP {StatusCode}.",
                    referenceType, referenceId, (int)response.StatusCode);
                throw new CommerceUnavailableException($"CommerceService answered HTTP {(int)response.StatusCode}.");
            }

            var body = await response.Content.ReadFromJsonAsync<OrderReviewReference>(Json, cancellationToken);
            return body ?? throw new CommerceUnavailableException("CommerceService returned an empty answer.");
        }
        catch (CommerceUnavailableException)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException
                                          || exception is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(exception,
                "[COMMERCE CLIENT] Review eligibility for {ReferenceType} '{ReferenceId}' could not be read.", referenceType, referenceId);
            throw new CommerceUnavailableException("CommerceService could not be reached.", exception);
        }
    }
}
