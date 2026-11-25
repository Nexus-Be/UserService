namespace User.Tests;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using User.Application.Interfaces;
using User.Infrastructure;
using User.Infrastructure.Services.Notification;
using Xunit;

public sealed class InfrastructureHttpClientDiTests
{
    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"notificationId":"notif-1"}""", Encoding.UTF8, "application/json")
            });
        }
    }

    private static ServiceProvider BuildInfrastructureProvider(
        Dictionary<string, string?> configOverrides,
        CapturingHttpMessageHandler capturingHandler,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        var baseConfig = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Port=5433;Database=usermanagement_db;Username=postgres;Password=postgres",
            ["InternalSecurity:ServiceKey"] = "test-internal-secret"
        };

        foreach (var kvp in configOverrides)
        {
            baseConfig[kvp.Key] = kvp.Value;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(baseConfig)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        if (httpContextAccessor != null)
        {
            services.AddSingleton(httpContextAccessor);
        }
        else
        {
            services.AddHttpContextAccessor();
        }

        services.AddInfrastructureServices(configuration);

        services.ConfigureAll<HttpClientFactoryOptions>(options =>
        {
            options.HttpMessageHandlerBuilderActions.Add(builder =>
            {
                builder.PrimaryHandler = capturingHandler;
            });
        });

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task EmailNotificationClient_has_service_identity_headers_and_InternalServiceAuthHandler()
    {
        var handler = new CapturingHttpMessageHandler();
        var context = new DefaultHttpContext { TraceIdentifier = "trace-otp-001" };
        context.Request.Headers["x-user-id"] = Guid.NewGuid().ToString();
        context.Request.Headers["x-user-roles"] = "buyer";
        var accessor = new HttpContextAccessor { HttpContext = context };

        using var provider = BuildInfrastructureProvider(new Dictionary<string, string?>(), handler, accessor);
        var client = provider.GetRequiredService<IEmailNotificationClient>();

        var sent = await client.SendOtpEmailAsync("guest@nexus.local", "Guest User", "123456");

        Assert.True(sent);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("http://localhost:8007/api/v1/notifications/email/otp", request.RequestUri?.ToString());
        Assert.Equal("00000000-0000-0000-0000-000000000001", string.Join(",", request.Headers.GetValues("x-user-id")));
        Assert.Equal("service", string.Join(",", request.Headers.GetValues("x-user-roles")));
        Assert.Equal("user-service", string.Join(",", request.Headers.GetValues("X-Caller-Service")));
        Assert.Equal("test-internal-secret", string.Join(",", request.Headers.GetValues("X-Internal-Service-Key")));
        Assert.Equal("trace-otp-001", string.Join(",", request.Headers.GetValues("x-trace-id")));
    }

    [Fact]
    public async Task NotificationRetry_named_client_has_service_identity_headers_and_InternalServiceAuthHandler()
    {
        var handler = new CapturingHttpMessageHandler();
        using var provider = BuildInfrastructureProvider(new Dictionary<string, string?>
        {
            ["Services:Notification:BaseUrl"] = "http://notif.local:8017"
        }, handler);

        var factory = provider.GetRequiredService<IHttpClientFactory>();
        using var client = factory.CreateClient(NotificationRetryWorker.HttpClientName);

        Assert.Equal(new Uri("http://notif.local:8017/"), client.BaseAddress);

        using var response = await client.PostAsync(
            "api/v1/notifications/password-changes",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("http://notif.local:8017/api/v1/notifications/password-changes", request.RequestUri?.ToString());
        Assert.Equal("00000000-0000-0000-0000-000000000001", string.Join(",", request.Headers.GetValues("x-user-id")));
        Assert.Equal("service", string.Join(",", request.Headers.GetValues("x-user-roles")));
        Assert.Equal("user-service", string.Join(",", request.Headers.GetValues("X-Caller-Service")));
        Assert.Equal("test-internal-secret", string.Join(",", request.Headers.GetValues("X-Internal-Service-Key")));
    }

    [Fact]
    public async Task WalletServiceClient_defaults_to_localhost_8008_and_attaches_InternalServiceKey()
    {
        var prevEnv = Environment.GetEnvironmentVariable("WALLET_SERVICE_URL");
        try
        {
            Environment.SetEnvironmentVariable("WALLET_SERVICE_URL", null);
            var handler = new CapturingHttpMessageHandler();
            using var provider = BuildInfrastructureProvider(new Dictionary<string, string?>(), handler);

            var walletClient = provider.GetRequiredService<IWalletServiceClient>();
            var ok = await walletClient.FreezeWalletAsync(Guid.NewGuid(), "DISPUTE");

            Assert.True(ok);
            var request = Assert.Single(handler.Requests);
            Assert.Equal("http://localhost:8008/api/v1/internal/wallets/freeze-purchase", request.RequestUri?.ToString());
            Assert.Equal("test-internal-secret", string.Join(",", request.Headers.GetValues("X-Internal-Service-Key")));
        }
        finally
        {
            Environment.SetEnvironmentVariable("WALLET_SERVICE_URL", prevEnv);
        }
    }

    [Fact]
    public async Task WalletServiceClient_env_var_takes_precedence_over_all_configuration_keys()
    {
        var prevEnv = Environment.GetEnvironmentVariable("WALLET_SERVICE_URL");
        try
        {
            Environment.SetEnvironmentVariable("WALLET_SERVICE_URL", "http://wallet-env:8008");
            var handler = new CapturingHttpMessageHandler();
            using var provider = BuildInfrastructureProvider(new Dictionary<string, string?>
            {
                ["WALLET_SERVICE_URL"] = "http://wallet-flat:8008",
                ["Services:Wallet:BaseUrl"] = "http://wallet-services:8008",
                ["WalletService:BaseUrl"] = "http://wallet-svc:8008",
                ["ExternalServices:WalletServiceUrl"] = "http://wallet-ext:8008"
            }, handler);

            var walletClient = provider.GetRequiredService<IWalletServiceClient>();
            await walletClient.FreezeWalletAsync(Guid.NewGuid(), "DISPUTE");

            var request = Assert.Single(handler.Requests);
            Assert.Equal("http://wallet-env:8008/api/v1/internal/wallets/freeze-purchase", request.RequestUri?.ToString());
        }
        finally
        {
            Environment.SetEnvironmentVariable("WALLET_SERVICE_URL", prevEnv);
        }
    }

    [Fact]
    public async Task WalletServiceClient_resolves_configuration_keys_in_strict_precedence_order()
    {
        var prevEnv = Environment.GetEnvironmentVariable("WALLET_SERVICE_URL");
        try
        {
            Environment.SetEnvironmentVariable("WALLET_SERVICE_URL", null);

            // 1. WALLET_SERVICE_URL beats Services:Wallet:BaseUrl, WalletService:BaseUrl, ExternalServices:WalletServiceUrl
            var handler1 = new CapturingHttpMessageHandler();
            using (var p1 = BuildInfrastructureProvider(new Dictionary<string, string?>
            {
                ["WALLET_SERVICE_URL"] = "http://wallet-flat:8008",
                ["Services:Wallet:BaseUrl"] = "http://wallet-services:8008",
                ["WalletService:BaseUrl"] = "http://wallet-svc:8008",
                ["ExternalServices:WalletServiceUrl"] = "http://wallet-ext:8008"
            }, handler1))
            {
                await p1.GetRequiredService<IWalletServiceClient>().FreezeWalletAsync(Guid.NewGuid(), "DISPUTE");
                Assert.Equal("http://wallet-flat:8008/api/v1/internal/wallets/freeze-purchase", Assert.Single(handler1.Requests).RequestUri?.ToString());
            }

            // 2. Services:Wallet:BaseUrl beats WalletService:BaseUrl, ExternalServices:WalletServiceUrl
            var handler2 = new CapturingHttpMessageHandler();
            using (var p2 = BuildInfrastructureProvider(new Dictionary<string, string?>
            {
                ["Services:Wallet:BaseUrl"] = "http://wallet-services:8008",
                ["WalletService:BaseUrl"] = "http://wallet-svc:8008",
                ["ExternalServices:WalletServiceUrl"] = "http://wallet-ext:8008"
            }, handler2))
            {
                await p2.GetRequiredService<IWalletServiceClient>().FreezeWalletAsync(Guid.NewGuid(), "DISPUTE");
                Assert.Equal("http://wallet-services:8008/api/v1/internal/wallets/freeze-purchase", Assert.Single(handler2.Requests).RequestUri?.ToString());
            }

            // 3. WalletService:BaseUrl beats ExternalServices:WalletServiceUrl
            var handler3 = new CapturingHttpMessageHandler();
            using (var p3 = BuildInfrastructureProvider(new Dictionary<string, string?>
            {
                ["WalletService:BaseUrl"] = "http://wallet-svc:8008",
                ["ExternalServices:WalletServiceUrl"] = "http://wallet-ext:8008"
            }, handler3))
            {
                await p3.GetRequiredService<IWalletServiceClient>().FreezeWalletAsync(Guid.NewGuid(), "DISPUTE");
                Assert.Equal("http://wallet-svc:8008/api/v1/internal/wallets/freeze-purchase", Assert.Single(handler3.Requests).RequestUri?.ToString());
            }

            // 4. ExternalServices:WalletServiceUrl is used before default
            var handler4 = new CapturingHttpMessageHandler();
            using (var p4 = BuildInfrastructureProvider(new Dictionary<string, string?>
            {
                ["ExternalServices:WalletServiceUrl"] = "http://wallet-ext:8008"
            }, handler4))
            {
                await p4.GetRequiredService<IWalletServiceClient>().FreezeWalletAsync(Guid.NewGuid(), "DISPUTE");
                Assert.Equal("http://wallet-ext:8008/api/v1/internal/wallets/freeze-purchase", Assert.Single(handler4.Requests).RequestUri?.ToString());
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("WALLET_SERVICE_URL", prevEnv);
        }
    }
}
