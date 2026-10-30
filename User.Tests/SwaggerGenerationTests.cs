namespace User.Tests;

using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Swashbuckle.AspNetCore.Swagger;
using Xunit;

public sealed class SwaggerGenerationTests
{
    private sealed class TestHostEnvironment : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "User.Api";
        public string WebRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    [Fact]
    public void Swagger_v1_specification_generates_successfully_without_route_conflicts()
    {
        var services = new ServiceCollection();

        var hostEnv = new TestHostEnvironment();
        services.AddSingleton<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>(hostEnv);
        services.AddSingleton<IHostEnvironment>(hostEnv);
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        services.AddRouting();
        services.AddControllers()
            .AddApplicationPart(typeof(User.Api.Controllers.AuthController).Assembly);

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        using var serviceProvider = services.BuildServiceProvider();
        var swaggerProvider = serviceProvider.GetRequiredService<ISwaggerProvider>();

        // Retrieve Swagger v1 doc - Swashbuckle throws if duplicate routes or conflicting actions exist
        var swaggerDoc = swaggerProvider.GetSwagger("v1");

        Assert.NotNull(swaggerDoc);
        Assert.NotNull(swaggerDoc.Paths);
        Assert.True(swaggerDoc.Paths.Count > 0, "Swagger specification should contain API paths.");
    }
}
