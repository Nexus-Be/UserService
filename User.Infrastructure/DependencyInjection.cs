namespace User.Infrastructure;

using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using User.Application.Interfaces;
using User.Infrastructure.Persistence;
using User.Infrastructure.Services.Auth;
using User.Infrastructure.Services.Notification;
using User.Infrastructure.Services.Redis;
using User.Infrastructure.Services.Storage;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var rawConnection = configuration.GetConnectionString("DefaultConnection")
            ?? configuration["CONNECTION_STRING"]
            ?? configuration["USER_CONNECTION_STRING"]
            ?? configuration["USER_DB_CONNECTION"]
            ?? Environment.GetEnvironmentVariable("USER_CONNECTION_STRING")
            ?? Environment.GetEnvironmentVariable("CONNECTION_STRING");

        if (string.IsNullOrWhiteSpace(rawConnection) || rawConnection.Contains("REPLACE_WITH"))
        {
            throw new InvalidOperationException("Database connection string for UserService is not configured. Set 'ConnectionStrings:DefaultConnection' or environment variable 'USER_CONNECTION_STRING'.");
        }
        var connectionString = NormalizeConnectionString(rawConnection);

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IUserDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddMemoryCache();

        // Redis
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var redisConn = configuration["Redis:ConnectionString"]
                ?? configuration["REDIS_CONNECTION_STRING"]
                ?? configuration.GetConnectionString("Redis")
                ?? Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING")
                ?? "localhost:6379";
            var redisOptions = ConfigurationOptions.Parse(redisConn);
            redisOptions.AbortOnConnectFail = false;
            redisOptions.ConnectRetry = 2;
            return ConnectionMultiplexer.Connect(redisOptions);
        });

        services.AddSingleton<IRedisOtpService, RedisOtpService>();
        services.AddSingleton<IJwtTokenGenerator, Services.Jwt.JwtTokenGenerator>();
        services.AddHttpClient<IFirebaseAuthService, FirebaseAuthService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        // Identity propagation delegating handler for outgoing inter-service HTTP requests
        services.AddTransient<Services.Common.IdentityPropagationHandler>();

        // REST Email Notification Client
        var notificationBaseUrl = Environment.GetEnvironmentVariable("NOTIFICATION_SERVICE_URL")
            ?? configuration["NOTIFICATION_SERVICE_URL"]
            ?? configuration["Services:Notification:BaseUrl"]
            ?? configuration["NotificationService:BaseUrl"]
            ?? "http://localhost:8007/";

        // Internal service key handler is temporarily disabled. Inter-service calls use plain REST routes.
        services.AddTransient<Services.Common.InternalServiceAuthHandler>();
        services.AddHttpClient<IEmailNotificationClient, EmailNotificationClient>(client =>
        {
            client.BaseAddress = new Uri(notificationBaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(10);
        }).AddHttpMessageHandler<Services.Common.IdentityPropagationHandler>();

        services.AddHttpClient(NotificationRetryWorker.HttpClientName, client =>
        {
            client.BaseAddress = new Uri(notificationBaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddHostedService<NotificationRetryWorker>();

        // REST Fulfillment Service Client (Internal sync with 5s timeout)
        var fulfillmentBaseUrl = Environment.GetEnvironmentVariable("FULFILLMENT_SERVICE_URL")
            ?? configuration["FULFILLMENT_SERVICE_URL"]
            ?? configuration["Services:Fulfillment:BaseUrl"]
            ?? configuration["FulfillmentService:BaseUrl"]
            ?? "http://localhost:8006/";

        services.AddHttpClient<IFulfillmentServiceClient, Services.External.FulfillmentServiceClient>(client =>
        {
            client.BaseAddress = new Uri(fulfillmentBaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(5);
        }).AddHttpMessageHandler<Services.Common.IdentityPropagationHandler>();

        // CommerceService: UserService asks it who took part in an order before it accepts a review.
        var commerceBaseUrl = Environment.GetEnvironmentVariable("COMMERCE_SERVICE_URL")
            ?? configuration["COMMERCE_SERVICE_URL"]
            ?? configuration["Services:Commerce:BaseUrl"]
            ?? configuration["CommerceService:BaseUrl"]
            ?? "http://localhost:8003/";
        var internalServiceKey = Environment.GetEnvironmentVariable("INTERNAL_SERVICE_SECRET")
            ?? configuration["INTERNAL_SERVICE_SECRET"]
            ?? configuration["InternalSecurity:ServiceKey"];

        services.AddHttpClient<ICommerceOrderClient, Services.External.CommerceOrderClient>(client =>
        {
            client.BaseAddress = new Uri(commerceBaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(5);
            if (!string.IsNullOrWhiteSpace(internalServiceKey))
                client.DefaultRequestHeaders.Add("X-Internal-Service-Key", internalServiceKey);
        });

        services.AddHttpClient<ICommerceServiceClient, Services.External.CommerceServiceClient>(client =>
        {
            client.BaseAddress = new Uri(commerceBaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(5);
        }).AddTypedClient<ICommerceServiceClient>((http, sp) => new Services.External.CommerceServiceClient(
            http,
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Services.External.CommerceServiceClient>>(),
            internalServiceKey))
          .AddHttpMessageHandler<Services.Common.IdentityPropagationHandler>();

        var walletBaseUrl = Environment.GetEnvironmentVariable("WALLET_SERVICE_URL")
            ?? configuration["ExternalServices:WalletServiceUrl"]
            ?? "http://wallet-service:8008";

        services.AddHttpClient<IWalletServiceClient, Services.External.WalletServiceClient>(client =>
        {
            client.BaseAddress = new Uri(walletBaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(5);
        }).AddHttpMessageHandler<Services.Common.InternalServiceAuthHandler>();

        // Image Moderation (Sightengine / AzureContentSafety when configured; pass-through otherwise)
        services.AddHttpClient<IImageModerationService, Services.Moderation.ConfiguredImageModerationService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        // File Storage (Cloudinary when configured, LocalFileStorageService fallback otherwise)
        var cloudName = Environment.GetEnvironmentVariable("CLOUDINARY_CLOUD_NAME")
            ?? configuration["Cloudinary:CloudName"];
        var cloudApiKey = Environment.GetEnvironmentVariable("CLOUDINARY_API_KEY")
            ?? configuration["Cloudinary:ApiKey"];
        var cloudApiSecret = Environment.GetEnvironmentVariable("CLOUDINARY_API_SECRET")
            ?? configuration["Cloudinary:ApiSecret"];

        if (!string.IsNullOrWhiteSpace(cloudName)
            && !string.IsNullOrWhiteSpace(cloudApiKey)
            && !string.IsNullOrWhiteSpace(cloudApiSecret))
        {
            services.AddSingleton<IFileStorageService>(sp =>
                new CloudinaryFileStorageService(
                    cloudName,
                    cloudApiKey,
                    cloudApiSecret,
                    sp.GetRequiredService<ILogger<CloudinaryFileStorageService>>()));
        }
        else
        {
            services.AddSingleton<IFileStorageService, LocalFileStorageService>();
        }

        return services;
    }

    private static string NormalizeConnectionString(string? connStr)
    {
        if (string.IsNullOrWhiteSpace(connStr)) return string.Empty;
        if (connStr.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) || 
            connStr.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(connStr);
            var userInfo = uri.UserInfo.Split(':');
            var user = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : "";
            var pass = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
            var host = uri.Host;
            var port = uri.Port > 0 ? uri.Port : 5432;
            var db = uri.AbsolutePath.TrimStart('/');
            return $"Host={host};Port={port};Database={db};Username={user};Password={pass};SSL Mode=Require;Trust Server Certificate=true";
        }
        return connStr;
    }
}
