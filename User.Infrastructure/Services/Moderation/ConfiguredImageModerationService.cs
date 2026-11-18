namespace User.Infrastructure.Services.Moderation;

using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using User.Application.Interfaces;

/// <summary>
/// Configurable image moderation client.
/// Returns allowed when IMAGE_MODERATION_PROVIDER is unset/None (local/dev safe default),
/// or evaluates via Sightengine / Azure Content Safety when configured.
/// </summary>
public sealed class ConfiguredImageModerationService : IImageModerationService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConfiguredImageModerationService> _logger;

    public ConfiguredImageModerationService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<ConfiguredImageModerationService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ImageModerationResult> ModerateAsync(
        Stream imageStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var provider = GetSetting("IMAGE_MODERATION_PROVIDER", "ImageModeration:Provider");

        if (string.IsNullOrWhiteSpace(provider) || string.Equals(provider, "None", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("[IMAGE MODERATION] Moderation skipped (provider='{Provider}').", provider ?? string.Empty);
            return new ImageModerationResult(true);
        }

        var apiKey = GetSetting("IMAGE_MODERATION_API_KEY", "ImageModeration:ApiKey");
        var apiSecret = GetSetting("IMAGE_MODERATION_API_SECRET", "ImageModeration:ApiSecret");
        var endpoint = GetSetting("IMAGE_MODERATION_ENDPOINT", "ImageModeration:Endpoint");

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("[IMAGE MODERATION] Provider '{Provider}' configured without IMAGE_MODERATION_API_KEY; skipping check.", provider);
            return new ImageModerationResult(true);
        }

        try
        {
            using var buffer = new MemoryStream();
            if (imageStream.CanSeek) imageStream.Position = 0;
            await imageStream.CopyToAsync(buffer, cancellationToken);
            if (imageStream.CanSeek) imageStream.Position = 0;

            if (string.Equals(provider, "Sightengine", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(apiSecret))
                {
                    _logger.LogWarning("[IMAGE MODERATION] Sightengine configured without IMAGE_MODERATION_API_SECRET; skipping check.");
                    return new ImageModerationResult(true);
                }
                return await ModerateWithSightengineAsync(buffer.ToArray(), fileName, contentType, apiKey, apiSecret, endpoint, cancellationToken);
            }

            if (string.Equals(provider, "AzureContentSafety", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(endpoint))
                {
                    _logger.LogWarning("[IMAGE MODERATION] AzureContentSafety configured without IMAGE_MODERATION_ENDPOINT; skipping check.");
                    return new ImageModerationResult(true);
                }
                return await ModerateWithAzureAsync(buffer.ToArray(), apiKey, endpoint, cancellationToken);
            }

            _logger.LogWarning("[IMAGE MODERATION] Unknown provider '{Provider}'; skipping check.", provider);
            return new ImageModerationResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[IMAGE MODERATION] Moderation request failed; allowing upload by fallback.");
            return new ImageModerationResult(true);
        }
    }

    private async Task<ImageModerationResult> ModerateWithSightengineAsync(
        byte[] bytes,
        string fileName,
        string contentType,
        string apiUser,
        string apiSecret,
        string? endpoint,
        CancellationToken cancellationToken)
    {
        var url = string.IsNullOrWhiteSpace(endpoint) ? "https://api.sightengine.com/1.0/check.json" : endpoint;
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(string.IsNullOrWhiteSpace(contentType) ? "image/png" : contentType);
        form.Add(fileContent, "media", fileName);
        form.Add(new StringContent("nudity-2.1,violence,offensive,wad"), "models");
        form.Add(new StringContent(apiUser), "api_user");
        form.Add(new StringContent(apiSecret), "api_secret");

        var response = await _httpClient.PostAsync(url, form, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return await AllowOnHttpFailureAsync("Sightengine", response, fileName, cancellationToken);
        }

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = doc.RootElement;

        var violence = root.TryGetProperty("violence", out var vEl) && vEl.TryGetProperty("prob", out var vp) ? vp.GetDouble() : 0;
        var offensive = root.TryGetProperty("offensive", out var oEl) && oEl.TryGetProperty("prob", out var op) ? op.GetDouble() : 0;
        var weapon = root.TryGetProperty("weapon", out var wEl) && wEl.ValueKind == JsonValueKind.Number ? wEl.GetDouble() : 0;
        var drugs = root.TryGetProperty("drugs", out var dEl) && dEl.ValueKind == JsonValueKind.Number ? dEl.GetDouble() : 0;
        var nudity = 0.0;
        if (root.TryGetProperty("nudity", out var nEl))
        {
            if (nEl.TryGetProperty("sexual_activity", out var sa)) nudity = Math.Max(nudity, sa.GetDouble());
            if (nEl.TryGetProperty("sexual_display", out var sd)) nudity = Math.Max(nudity, sd.GetDouble());
            if (nEl.TryGetProperty("erotica", out var er)) nudity = Math.Max(nudity, er.GetDouble());
            if (nEl.TryGetProperty("very_suggestive", out var vs)) nudity = Math.Max(nudity, vs.GetDouble());
        }

        _logger.LogInformation(
            "[IMAGE MODERATION] Sightengine scores for '{FileName}': nudity={Nudity:F2}, violence={Violence:F2}, offensive={Offensive:F2}, weapon={Weapon:F2}, drugs={Drugs:F2}",
            fileName, nudity, violence, offensive, weapon, drugs);

        if (nudity >= 0.70)
            return new ImageModerationResult(false, "Explicit", nudity, "Explicit or sexually suggestive image detected.");
        if (violence >= 0.70)
            return new ImageModerationResult(false, "Violence", violence, "Violent or graphic image detected.");
        if (weapon >= 0.60)
            return new ImageModerationResult(false, "Weapon", weapon, "Weapon detected in image.");
        if (drugs >= 0.75)
            return new ImageModerationResult(false, "Drugs", drugs, "Drug-related content detected in image.");
        if (offensive >= 0.70)
            return new ImageModerationResult(false, "Hate", offensive, "Offensive or hate-speech imagery detected.");

        return new ImageModerationResult(true);
    }

    private async Task<ImageModerationResult> ModerateWithAzureAsync(
        byte[] bytes,
        string apiKey,
        string endpoint,
        CancellationToken cancellationToken)
    {
        var url = $"{endpoint.TrimEnd('/')}/contentsafety/image:analyze?api-version=2023-10-01";
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Add("Ocp-Apim-Subscription-Key", apiKey);
        var body = JsonSerializer.Serialize(new { image = new { content = Convert.ToBase64String(bytes) } });
        req.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(req, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return await AllowOnHttpFailureAsync("AzureContentSafety", response, null, cancellationToken);
        }

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (doc.RootElement.TryGetProperty("categoriesAnalysis", out var cats) && cats.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in cats.EnumerateArray())
            {
                var severity = item.TryGetProperty("severity", out var sEl) ? sEl.GetInt32() : 0;
                var category = item.TryGetProperty("category", out var cEl) ? cEl.GetString() ?? "Violation" : "Violation";
                if (severity >= 4)
                {
                    return new ImageModerationResult(false, category, severity / 6.0, $"Image violates community standards ({category}).");
                }
            }
        }

        return new ImageModerationResult(true);
    }

    private string GetSetting(string envVarName, string configKey)
    {
        var envVal = Environment.GetEnvironmentVariable(envVarName);
        if (!string.IsNullOrWhiteSpace(envVal))
        {
            return envVal.Trim();
        }

        return (_configuration[configKey] ?? string.Empty).Trim();
    }

    private async Task<ImageModerationResult> AllowOnHttpFailureAsync(
        string provider,
        HttpResponseMessage response,
        string? fileName,
        CancellationToken cancellationToken)
    {
        var errBody = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogWarning(
            "[IMAGE MODERATION] {Provider} returned HTTP {StatusCode} (file={FileName}): {Body}",
            provider, (int)response.StatusCode, fileName ?? "n/a", errBody);
        return new ImageModerationResult(true);
    }
}
