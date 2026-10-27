using Microsoft.EntityFrameworkCore;
using User.Api.Extensions;
using User.Api.Middleware;
using User.Application;
using User.Infrastructure;
using User.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// 0. Observability / OpenTelemetry Tracing
builder.Services.AddServiceObservability(builder.Configuration, "user-service");

// 1. Application & Infrastructure Dependency Injection
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// 2. REST Services
builder.Services.AddControllers();

// 3. Swagger / OpenAPI & JSON Serialization
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new User.Api.Common.Converters.PascalCaseJsonStringEnumConverter(allowIntegerValues: false));
});
builder.Services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options =>
{
    options.JsonSerializerOptions.Converters.Add(new User.Api.Common.Converters.PascalCaseJsonStringEnumConverter(allowIntegerValues: false));
});

var app = builder.Build();

// 4. Global Exception Handler Middleware
app.UseGlobalExceptionHandler();

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

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "User.Api" }));

app.Run();
