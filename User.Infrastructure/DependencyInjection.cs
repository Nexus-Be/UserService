namespace User.Infrastructure;

using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using User.Application.Consumers;
using User.Application.Interfaces;
using User.Infrastructure.Persistence;
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

        // MassTransit RabbitMQ
        services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();

            x.AddApplicationConsumers();

            x.AddEntityFrameworkOutbox<AppDbContext>(o =>
            {
                o.UsePostgres();
                o.UseBusOutbox();
            });

            x.UsingRabbitMq((context, cfg) =>
            {
                var host = configuration["RabbitMq:Host"] ?? "localhost";
                var portStr = configuration["RabbitMq:Port"] ?? "5672";
                var port = ushort.TryParse(portStr, out var p) ? p : (ushort)5672;
                var user = configuration["RabbitMq:User"] ?? "guest";
                var pass = configuration["RabbitMq:Password"] ?? "guest";

                cfg.Host(host, port, "/", h =>
                {
                    h.Username(user);
                    h.Password(pass);
                });

                cfg.ConfigureEndpoints(context);
            });
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
