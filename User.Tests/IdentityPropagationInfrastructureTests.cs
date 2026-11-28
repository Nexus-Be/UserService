namespace User.Tests;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using User.Api.Middleware;
using User.Infrastructure.Services.Common;
using Xunit;

public sealed class IdentityPropagationInfrastructureTests
{
    [Fact]
    public async Task UserContextMiddleware_populates_ClaimsPrincipal_when_headers_present()
    {
        var context = new DefaultHttpContext();
        var expectedUserId = Guid.NewGuid();
        context.Request.Headers["x-user-id"] = expectedUserId.ToString();
        context.Request.Headers["x-user-roles"] = "seller,buyer";
        context.Request.Headers["x-user-email"] = "test@nexus.local";
        context.Request.Headers["x-user-permissions"] = "PRODUCTS_CREATE,PRODUCTS_UPDATE";

        var nextInvoked = false;
        var middleware = new UserContextMiddleware(nextContext =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.True(nextInvoked);
        Assert.NotNull(context.User);
        Assert.True(context.User.Identity?.IsAuthenticated);
        Assert.Equal("NexusGatewayAuth", context.User.Identity?.AuthenticationType);
        Assert.Equal(expectedUserId.ToString(), context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal("test@nexus.local", context.User.FindFirst(ClaimTypes.Email)?.Value);
        Assert.True(context.User.IsInRole("seller"));
        Assert.True(context.User.IsInRole("buyer"));
        Assert.False(context.User.IsInRole("admin"));
        Assert.Contains(context.User.FindAll("permission"), c => c.Value == "PRODUCTS_CREATE");
        Assert.Contains(context.User.FindAll("permission"), c => c.Value == "PRODUCTS_UPDATE");
    }

    [Fact]
    public async Task UserContextMiddleware_does_not_modify_user_when_userId_header_missing()
    {
        var context = new DefaultHttpContext();
        var nextInvoked = false;
        var middleware = new UserContextMiddleware(nextContext =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.True(nextInvoked);
        Assert.False(context.User.Identity?.IsAuthenticated ?? false);
    }

    [Fact]
    public async Task UserContextMiddleware_does_not_modify_user_when_userId_header_invalid_guid()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["x-user-id"] = "not-a-valid-guid";

        var nextInvoked = false;
        var middleware = new UserContextMiddleware(nextContext =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.True(nextInvoked);
        Assert.False(context.User.Identity?.IsAuthenticated ?? false);
    }

    private sealed class TestCapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    [Fact]
    public async Task IdentityPropagationHandler_injects_headers_from_ambient_HttpContext()
    {
        var expectedUserId = Guid.NewGuid();
        var context = new DefaultHttpContext();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, expectedUserId.ToString()),
            new(ClaimTypes.Role, "seller"),
            new(ClaimTypes.Role, "buyer"),
            new(ClaimTypes.Email, "ambient@nexus.local"),
            new("permission", "USERS_READ"),
            new("permission", "USERS_WRITE")
        };
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "NexusGatewayAuth"));
        context.TraceIdentifier = "trace-ambient-12345";
        context.Request.Headers["x-client-ip"] = "192.168.1.100";

        var httpContextAccessor = new HttpContextAccessor { HttpContext = context };
        var capturingHandler = new TestCapturingHandler();
        var handler = new IdentityPropagationHandler(httpContextAccessor, NullLogger<IdentityPropagationHandler>.Instance)
        {
            InnerHandler = capturingHandler
        };

        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://downstream.local/")
        };

        var response = await client.GetAsync("api/v1/resource");

        Assert.NotNull(capturingHandler.LastRequest);
        Assert.True(capturingHandler.LastRequest!.Headers.Contains("x-user-id"));
        Assert.Equal(expectedUserId.ToString(), string.Join(",", capturingHandler.LastRequest.Headers.GetValues("x-user-id")));

        Assert.True(capturingHandler.LastRequest.Headers.Contains("x-user-roles"));
        Assert.Equal("seller,buyer", string.Join(",", capturingHandler.LastRequest.Headers.GetValues("x-user-roles")));

        Assert.True(capturingHandler.LastRequest.Headers.Contains("x-user-email"));
        Assert.Equal("ambient@nexus.local", string.Join(",", capturingHandler.LastRequest.Headers.GetValues("x-user-email")));

        Assert.True(capturingHandler.LastRequest.Headers.Contains("x-user-permissions"));
        Assert.Equal("USERS_READ,USERS_WRITE", string.Join(",", capturingHandler.LastRequest.Headers.GetValues("x-user-permissions")));

        Assert.True(capturingHandler.LastRequest.Headers.Contains("x-client-ip"));
        Assert.Equal("192.168.1.100", string.Join(",", capturingHandler.LastRequest.Headers.GetValues("x-client-ip")));

        Assert.True(capturingHandler.LastRequest.Headers.Contains("x-trace-id"));
        Assert.Equal("trace-ambient-12345", string.Join(",", capturingHandler.LastRequest.Headers.GetValues("x-trace-id")));
    }

    [Fact]
    public async Task IdentityPropagationHandler_falls_back_to_raw_headers_if_User_is_empty()
    {
        var expectedUserId = Guid.NewGuid();
        var context = new DefaultHttpContext();
        context.Request.Headers["x-user-id"] = expectedUserId.ToString();
        context.Request.Headers["x-user-roles"] = "admin";
        context.Request.Headers["x-user-email"] = "admin@nexus.local";

        var httpContextAccessor = new HttpContextAccessor { HttpContext = context };
        var capturingHandler = new TestCapturingHandler();
        var handler = new IdentityPropagationHandler(httpContextAccessor, NullLogger<IdentityPropagationHandler>.Instance)
        {
            InnerHandler = capturingHandler
        };

        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://downstream.local/")
        };

        var response = await client.GetAsync("api/v1/resource");

        Assert.NotNull(capturingHandler.LastRequest);
        Assert.Equal(expectedUserId.ToString(), string.Join(",", capturingHandler.LastRequest!.Headers.GetValues("x-user-id")));
        Assert.Equal("admin", string.Join(",", capturingHandler.LastRequest.Headers.GetValues("x-user-roles")));
        Assert.Equal("admin@nexus.local", string.Join(",", capturingHandler.LastRequest.Headers.GetValues("x-user-email")));
    }

    [Fact]
    public async Task IdentityPropagationHandler_passes_through_gracefully_when_HttpContext_is_null()
    {
        var httpContextAccessor = new HttpContextAccessor { HttpContext = null };
        var capturingHandler = new TestCapturingHandler();
        var handler = new IdentityPropagationHandler(httpContextAccessor, NullLogger<IdentityPropagationHandler>.Instance)
        {
            InnerHandler = capturingHandler
        };

        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://downstream.local/")
        };

        var response = await client.GetAsync("api/v1/resource");

        Assert.NotNull(capturingHandler.LastRequest);
        Assert.False(capturingHandler.LastRequest!.Headers.Contains("x-user-id"));
        Assert.False(capturingHandler.LastRequest.Headers.Contains("x-user-roles"));
    }

    [Fact]
    public async Task IdentityPropagationHandler_does_not_overwrite_manually_set_headers()
    {
        var ambientUserId = Guid.NewGuid();
        var explicitUserId = Guid.NewGuid();

        var context = new DefaultHttpContext();
        context.Request.Headers["x-user-id"] = ambientUserId.ToString();

        var httpContextAccessor = new HttpContextAccessor { HttpContext = context };
        var capturingHandler = new TestCapturingHandler();
        var handler = new IdentityPropagationHandler(httpContextAccessor, NullLogger<IdentityPropagationHandler>.Instance)
        {
            InnerHandler = capturingHandler
        };

        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://downstream.local/")
        };

        using var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/resource");
        request.Headers.Add("x-user-id", explicitUserId.ToString());

        await client.SendAsync(request);

        Assert.NotNull(capturingHandler.LastRequest);
        Assert.Equal(explicitUserId.ToString(), string.Join(",", capturingHandler.LastRequest!.Headers.GetValues("x-user-id")));
    }
}
