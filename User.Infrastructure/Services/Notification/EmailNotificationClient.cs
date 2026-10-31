namespace User.Infrastructure.Services.Notification;

using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using User.Application.Interfaces;

public sealed class EmailNotificationClient : IEmailNotificationClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EmailNotificationClient> _logger;

    public EmailNotificationClient(HttpClient httpClient, ILogger<EmailNotificationClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> SendOtpEmailAsync(
        string email,
        string fullName,
        string otpCode,
        int expiresInMinutes = 5,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new
            {
                email = email.Trim(),
                fullName = fullName.Trim(),
                otpCode = otpCode.Trim(),
                expiresInMinutes
            };

            var response = await _httpClient.PostAsJsonAsync("api/v1/internal/notifications/email/otp", payload, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[REST Email Notification] OTP email dispatched successfully to '{Email}'.", email);
                return true;
            }

            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("[REST Email Notification] Failed to send OTP email to '{Email}'. Status: {StatusCode}, Details: {Details}",
                email, response.StatusCode, errorBody);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[REST Email Notification] Connection error while dispatching OTP email to '{Email}'.", email);
            return false;
        }
    }
}
