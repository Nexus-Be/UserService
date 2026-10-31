namespace User.Tests;

using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.OpenApi.Models;
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

    private static ISwaggerProvider CreateSwaggerProvider()
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

        var serviceProvider = services.BuildServiceProvider();
        return serviceProvider.GetRequiredService<ISwaggerProvider>();
    }

    [Fact]
    public void Swagger_v1_specification_generates_successfully_without_route_conflicts()
    {
        var swaggerProvider = CreateSwaggerProvider();

        // Retrieve Swagger v1 doc - Swashbuckle throws if duplicate routes or conflicting actions exist
        var swaggerDoc = swaggerProvider.GetSwagger("v1");

        Assert.NotNull(swaggerDoc);
        Assert.NotNull(swaggerDoc.Paths);
        Assert.True(swaggerDoc.Paths.Count > 0, "Swagger specification should contain API paths.");
    }

    [Fact]
    public void Admin_users_list_uses_the_shared_pageSize_query_parameter()
    {
        var swaggerDoc = CreateSwaggerProvider().GetSwagger("v1");

        var path = swaggerDoc.Paths["/api/v1/admin/users"];
        var operation = Assert.IsType<OpenApiOperation>(path.Operations[OperationType.Get]);
        var parameterNames = operation.Parameters.Select(parameter => parameter.Name).ToList();

        Assert.Contains("pageSize", parameterNames);
        Assert.DoesNotContain("limit", parameterNames);
    }

    [Fact]
    public void Admin_users_delete_declares_only_the_success_status_code()
    {
        var swaggerDoc = CreateSwaggerProvider().GetSwagger("v1");

        var operation = Assert.IsType<OpenApiOperation>(
            swaggerDoc.Paths["/api/v1/admin/users/{userId}"].Operations[OperationType.Delete]);

        Assert.Contains("204", operation.Responses.Keys);
        Assert.DoesNotContain("400", operation.Responses.Keys);
        Assert.DoesNotContain("404", operation.Responses.Keys);
    }
}
