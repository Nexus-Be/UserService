namespace User.Infrastructure;

using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using User.Application.Interfaces;
using User.Infrastructure.Persistence;
using User.Infrastructure.Services.Redis;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5433;Database=usermanagement_db;Username=postgres;Password=MatkhaucuaTui123@";

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
}
