using MassTransit;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using User.Api.Services;
using User.Application.Interfaces;
using User.Infrastructure.Persistence;
using User.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Cấu hình Kestrel chỉ định HTTP/2 cho gRPC không cần TLS (h2c)
builder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureEndpointDefaults(listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
    });
});

// 1. CSDL PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5433;Database=usermanagement_db;Username=postgres;Password=MatkhaucuaTui123@";
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// 2. Redis Multiplexer cho OTP Service (Fail-safe khi chưa bật Redis)
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var redisConn = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379";
    var redisOptions = ConfigurationOptions.Parse(redisConn);
    redisOptions.AbortOnConnectFail = false;
    redisOptions.ConnectRetry = 2;
    return ConnectionMultiplexer.Connect(redisOptions);
});

// 3. DI Services (Code nghiệp vụ trực tiếp, KHÔNG dùng MediatR)
builder.Services.AddSingleton<IRedisOtpService, RedisOtpService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// 4. MassTransit RabbitMQ Publisher
builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();

    x.UsingRabbitMq((context, cfg) =>
    {
        var host = builder.Configuration["RabbitMq:Host"] ?? "localhost";
        var portStr = builder.Configuration["RabbitMq:Port"] ?? "5672";
        var port = ushort.TryParse(portStr, out var p) ? p : (ushort)5672;
        var user = builder.Configuration["RabbitMq:User"] ?? "guest";
        var pass = builder.Configuration["RabbitMq:Password"] ?? "guest";

        cfg.Host(host, port, "/", h =>
        {
            h.Username(user);
            h.Password(pass);
        });

        cfg.ConfigureEndpoints(context);
    });
});

// 5. gRPC Services
builder.Services.AddGrpc();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Tự động apply migrations nếu kết nối CSDL thành công
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        if (db.Database.IsRelational())
        {
            db.Database.Migrate();
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning("Database migration deferred on startup: {Message}", ex.Message);
    }
}

app.MapGrpcService<UsersGrpcService>();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "User.Api" }));

app.Run();
