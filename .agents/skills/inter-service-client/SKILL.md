---
name: inter-service-client
description: Add or modify outbound typed REST clients in UserService (port 8001). Use when UserService must call downstream services such as NotificationService without using message brokers.
---

# Outbound REST Client in UserService

> Scope: UserService repository only. Never reference parent workspace paths, root documentation, or sibling service directories.

## Local Facts

- Service: UserService
- Local HTTP Port: 8001
- Primary Downstream Target: NotificationService (Port 8007)
- Interface Location: `User.Application/Interfaces/`
- Implementation Location: `User.Infrastructure/Services/{Target}/`
- DI Registration: `User.Infrastructure/DependencyInjection.cs`
- Existing Downstream Client: `IEmailNotificationClient` -> `POST api/v1/notifications/email/otp`

## Invariants

1. **100% RESTful HTTP/JSON:** No RabbitMQ, MassTransit, Saga, or Outbox. All cross-service calls use typed `HttpClient`.
2. **Strict Inward-Only Scoping:** Do not reference assemblies, project files, or databases of other services.
3. **Public Endpoint Conventions:** The internal API key mechanism is disabled. Target routes use standard prefixes (e.g., `POST api/v1/notifications/email/otp`). Do not send `X-Internal-Service-Key`.
4. **Timeout & Failure Handling:** Enforce `client.Timeout = TimeSpan.FromSeconds(10)`. Wrap outbound calls in `try-catch`, log failure details, and prevent downstream network outages from aborting core registration or authentication flows when a fallback exists.

## Implementation Steps

### 1. Application Contract
Define `User.Application/Interfaces/I{Target}Client.cs`:
```csharp
namespace User.Application.Interfaces;

public interface IEmailNotificationClient
{
    Task<bool> SendOtpEmailAsync(
        string email,
        string fullName,
        string otpCode,
        int expiresInMinutes = 5,
        CancellationToken cancellationToken = default);
}
```

### 2. Infrastructure Typed Client
Implement `User.Infrastructure/Services/Notification/{Target}Client.cs`:
```csharp
namespace User.Infrastructure.Services.Notification;

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
            var payload = new { email, fullName, otpCode, expiresInMinutes };
            var response = await _httpClient.PostAsJsonAsync("api/v1/notifications/email/otp", payload, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("[REST Notification] Failed dispatch: {Status} {Error}", response.StatusCode, error);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[REST Notification] Connection error to NotificationService.");
            return false;
        }
    }
}
```

### 3. Dependency Injection Registration
In `User.Infrastructure/DependencyInjection.cs`:
```csharp
var notificationBaseUrl = Environment.GetEnvironmentVariable("NOTIFICATION_SERVICE_URL")
    ?? configuration["NOTIFICATION_SERVICE_URL"]
    ?? configuration["Services:Notification:BaseUrl"]
    ?? "http://localhost:8007/";

services.AddHttpClient<EmailNotificationClient>(client =>
{
    client.BaseAddress = new Uri(notificationBaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(10);
});
```

### 4. Configuration Synchronization
Ensure `NOTIFICATION_SERVICE_URL: "${NOTIFICATION_SERVICE_URL:-http://localhost:8007/}"` is declared in `docker-compose.yml`, `.env.example`, and `.env.server.example`.
