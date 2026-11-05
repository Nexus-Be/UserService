namespace User.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using User.Api.Middleware;
using Xunit;

public sealed class InternalServiceAuthMiddlewareTests
{
    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "UserService";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = default!;
    }

    [Fact]
    public async Task InvokeAsync_WhenNonInternalRoute_PassesThrough()
    {
        var nextCalled = false;
        RequestDelegate next = (ctx) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var config = new ConfigurationBuilder().Build();
        var env = new FakeHostEnvironment();
        var middleware = new InternalServiceAuthMiddleware(next, config, env, NullLogger<InternalServiceAuthMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/users/me";

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_WhenInternalRouteWithValidKey_CallsNext()
    {
        var nextCalled = false;
        RequestDelegate next = (ctx) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var secret = "test-internal-secret-key-12345";
        var inMemoryConfig = new Dictionary<string, string?>
        {
            ["INTERNAL_SERVICE_SECRET"] = secret
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();
        var env = new FakeHostEnvironment();
        var middleware = new InternalServiceAuthMiddleware(next, config, env, NullLogger<InternalServiceAuthMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/internal/users/verify-bid-eligibility";
        context.Request.Headers[InternalServiceAuthMiddleware.HeaderKeyInternalService] = secret;

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_WhenInternalRouteWithInvalidKey_Returns403()
    {
        var nextCalled = false;
        RequestDelegate next = (ctx) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var inMemoryConfig = new Dictionary<string, string?>
        {
            ["INTERNAL_SERVICE_SECRET"] = "correct-secret"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();
        var env = new FakeHostEnvironment();
        var middleware = new InternalServiceAuthMiddleware(next, config, env, NullLogger<InternalServiceAuthMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Path = "/api/v1/internal/users/check-reputation";
        context.Request.Headers[InternalServiceAuthMiddleware.HeaderKeyInternalService] = "wrong-secret";

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenInternalRouteWithMissingKey_Returns403()
    {
        var nextCalled = false;
        RequestDelegate next = (ctx) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var inMemoryConfig = new Dictionary<string, string?>
        {
            ["INTERNAL_SERVICE_SECRET"] = "correct-secret"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();
        var env = new FakeHostEnvironment();
        var middleware = new InternalServiceAuthMiddleware(next, config, env, NullLogger<InternalServiceAuthMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Path = "/api/v1/internal/users/batch-public-info";

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }
}
