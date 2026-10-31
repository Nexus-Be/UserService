namespace User.Tests;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using User.Infrastructure.Services.Common;
using User.Infrastructure.Services.Notification;
using Xunit;

public sealed class EmailNotificationClientTests
{
    [Fact]
    public async Task SendOtpEmailAsync_posts_to_internal_endpoint_with_internal_service_key()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["InternalSecurity:ServiceKey"] = "integration-test-key"
            })
            .Build();

        var capture = new CapturingHandler();
        var authHandler = new InternalServiceAuthHandler(
            configuration,
            NullLogger<InternalServiceAuthHandler>.Instance)
        {
            InnerHandler = capture
        };

        using var httpClient = new HttpClient(authHandler)
        {
            BaseAddress = new Uri("http://notification.local/")
        };

        var client = new EmailNotificationClient(httpClient, NullLogger<EmailNotificationClient>.Instance);

        var sent = await client.SendOtpEmailAsync("buyer@nexus.local", "Buyer Test", "654321", 5);

        Assert.True(sent);
        Assert.NotNull(capture.LastRequest);
        Assert.Equal(HttpMethod.Post, capture.LastRequest!.Method);
        Assert.Equal("/api/v1/internal/notifications/email/otp", capture.LastRequest.RequestUri!.AbsolutePath);
        Assert.True(capture.LastRequest.Headers.Contains(InternalServiceAuthHandler.HeaderKeyInternalService));
        Assert.Contains("654321", capture.LastBody);
    }

    [Fact]
    public async Task QueuedEmailNotificationClient_enqueues_message_and_returns_true()
    {
        var queue = new OtpEmailQueue();
        var client = new QueuedEmailNotificationClient(queue, NullLogger<QueuedEmailNotificationClient>.Instance);

        var sent = await client.SendOtpEmailAsync("queued@nexus.local", "Queued User", "112233", 10);
        var message = await queue.DequeueAsync(CancellationToken.None);

        Assert.True(sent);
        Assert.Equal("queued@nexus.local", message.Email);
        Assert.Equal("Queued User", message.FullName);
        Assert.Equal("112233", message.OtpCode);
        Assert.Equal(10, message.ExpiresInMinutes);
    }

    [Fact]
    public async Task QueuedEmailNotificationClient_returns_false_when_queue_is_full()
    {
        var queue = new OtpEmailQueue();
        var client = new QueuedEmailNotificationClient(queue, NullLogger<QueuedEmailNotificationClient>.Instance);

        for (var index = 0; index < 1000; index++)
        {
            var accepted = await client.SendOtpEmailAsync("queued@nexus.local", "Queued User", "112233", 5);
            Assert.True(accepted);
        }

        var overflow = await client.SendOtpEmailAsync("queued@nexus.local", "Queued User", "112233", 5);
        Assert.False(overflow);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
