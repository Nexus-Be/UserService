namespace User.Infrastructure;

using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using User.Application.Interfaces;
using User.Infrastructure.Persistence;
using User.Infrastructure.Services.Notification;
using User.Infrastructure.Services.Redis;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var rawConnection = configuration.GetConnectionString("DefaultConnection")
            ?? configuration["CONNECTION_STRING"]
            ?? "Host=localhost;Port=5433;Database=usermanagement_db;Username=postgres;Password=postgres";
        var connectionString = NormalizeConnectionString(rawConnection);

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IUserDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        // Redis
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var redisConn = configuration["Redis:ConnectionString"] ?? "localhost:6379";
            var redisOptions = ConfigurationOptions.Parse(redisConn);
            redisOptions.AbortOnConnectFail = false;
            redisOptions.ConnectRetry = 2;
            return ConnectionMultiplexer.Connect(redisOptions);
        });

        services.AddSingleton<IRedisOtpService, RedisOtpService>();
        services.AddSingleton<IJwtTokenGenerator, Services.Jwt.JwtTokenGenerator>();

        // REST Email Notification Client
        var notificationBaseUrl = configuration["Services:Notification:BaseUrl"]
            ?? configuration["NotificationService:BaseUrl"]
            ?? "http://localhost:8007/";

        services.AddHttpClient<IEmailNotificationClient, EmailNotificationClient>(client =>
        {
            client.BaseAddress = new Uri(notificationBaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });

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
