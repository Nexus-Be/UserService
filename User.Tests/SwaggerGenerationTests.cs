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

    [Fact]
    public void Admin_users_ban_and_unban_are_declared_in_swagger()
    {
        var swaggerDoc = CreateSwaggerProvider().GetSwagger("v1");

        Assert.True(swaggerDoc.Paths.ContainsKey("/api/v1/admin/users/{userId}/ban"));
        var banOp = swaggerDoc.Paths["/api/v1/admin/users/{userId}/ban"].Operations[OperationType.Patch];
        Assert.NotNull(banOp);
        Assert.Contains("200", banOp.Responses.Keys);

        Assert.True(swaggerDoc.Paths.ContainsKey("/api/v1/admin/users/{userId}/unban"));
        var unbanOp = swaggerDoc.Paths["/api/v1/admin/users/{userId}/unban"].Operations[OperationType.Patch];
        Assert.NotNull(unbanOp);
        Assert.Contains("200", unbanOp.Responses.Keys);
    }

    [Fact]
    public void Reputation_reviews_reports_configs_and_internal_routes_are_declared_in_swagger()
    {
        var swaggerDoc = CreateSwaggerProvider().GetSwagger("v1");

        Assert.True(swaggerDoc.Paths.ContainsKey("/api/v1/me/reputation"));
        Assert.True(swaggerDoc.Paths.ContainsKey("/api/v1/me/penalties"));
        Assert.True(swaggerDoc.Paths.ContainsKey("/api/v1/me/reports"));
        Assert.True(swaggerDoc.Paths.ContainsKey("/api/v1/users/{userId}/reputation"));
        Assert.True(swaggerDoc.Paths.ContainsKey("/api/v1/users/{userId}/reputation/history"));
        Assert.True(swaggerDoc.Paths.ContainsKey("/api/v1/users/{userId}/reviews"));
        Assert.True(swaggerDoc.Paths.ContainsKey("/api/v1/users/reports"));
        Assert.True(swaggerDoc.Paths.ContainsKey("/api/v1/reviews"));
        Assert.True(swaggerDoc.Paths.ContainsKey("/api/v1/reputation/policy"));
        Assert.True(swaggerDoc.Paths.ContainsKey("/api/v1/admin/users/{userId}/reputation"));
        Assert.True(swaggerDoc.Paths.ContainsKey("/api/v1/admin/reviews/{reviewId}/hide"));
        Assert.True(swaggerDoc.Paths.ContainsKey("/api/v1/admin/reports"));
        Assert.True(swaggerDoc.Paths.ContainsKey("/api/v1/admin/reports/{reportId}/resolve"));
        Assert.True(swaggerDoc.Paths.ContainsKey("/api/v1/admin/reports/{reportId}/dismiss"));
        Assert.True(swaggerDoc.Paths.ContainsKey("/api/v1/admin/configs"));
        Assert.True(swaggerDoc.Paths.ContainsKey("/api/v1/internal/users/check-reputation"));
        Assert.True(swaggerDoc.Paths.ContainsKey("/api/v1/internal/users/verify-bid-eligibility"));
        Assert.True(swaggerDoc.Paths.ContainsKey("/api/v1/internal/users/batch-public-info"));
        Assert.True(swaggerDoc.Paths.ContainsKey("/api/v1/internal/users/penalties"));
        Assert.True(swaggerDoc.Paths.ContainsKey("/api/v1/internal/users/reputation-adjust"));
    }
}
