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
    public async Task SendOtpEmailAsync_posts_to_public_endpoint_without_internal_service_key()
    {
        var capture = new CapturingHandler();

        using var httpClient = new HttpClient(capture)
        {
            BaseAddress = new Uri("http://notification.local/")
        };

        var client = new EmailNotificationClient(httpClient, NullLogger<EmailNotificationClient>.Instance);

        var sent = await client.SendOtpEmailAsync("buyer@nexus.local", "Buyer Test", "654321", 5);

        Assert.True(sent);
        Assert.NotNull(capture.LastRequest);
        Assert.Equal(HttpMethod.Post, capture.LastRequest!.Method);
        Assert.Equal("/api/v1/notifications/email/otp", capture.LastRequest.RequestUri!.AbsolutePath);
        Assert.False(capture.LastRequest.Headers.Contains(InternalServiceAuthHandler.HeaderKeyInternalService));
        Assert.Contains("654321", capture.LastBody);
    }

    [Fact]
    public async Task SendSellerApprovedEmailAsync_posts_to_seller_approved_endpoint()
    {
        var capture = new CapturingHandler();

        using var httpClient = new HttpClient(capture)
        {
            BaseAddress = new Uri("http://notification.local/")
        };

        var client = new EmailNotificationClient(httpClient, NullLogger<EmailNotificationClient>.Instance);

        var sent = await client.SendSellerApprovedEmailAsync("shop@nexus.local", "Seller Test", "Nexus Shop");

        Assert.True(sent);
        Assert.NotNull(capture.LastRequest);
        Assert.Equal(HttpMethod.Post, capture.LastRequest!.Method);
        Assert.Equal("/api/v1/notifications/email/seller-approved", capture.LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("shop@nexus.local", capture.LastBody);
        Assert.Contains("Nexus Shop", capture.LastBody);
    }

    [Fact]
    public async Task SendSellerRejectedEmailAsync_posts_to_seller_rejected_endpoint_with_reason()
    {
        var capture = new CapturingHandler();

        using var httpClient = new HttpClient(capture)
        {
            BaseAddress = new Uri("http://notification.local/")
        };

        var client = new EmailNotificationClient(httpClient, NullLogger<EmailNotificationClient>.Instance);

        var sent = await client.SendSellerRejectedEmailAsync("shop@nexus.local", "Seller Test", "Nexus Shop", "Blurred ID card");

        Assert.True(sent);
        Assert.NotNull(capture.LastRequest);
        Assert.Equal(HttpMethod.Post, capture.LastRequest!.Method);
        Assert.Equal("/api/v1/notifications/email/seller-rejected", capture.LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("shop@nexus.local", capture.LastBody);
        Assert.Contains("Blurred ID card", capture.LastBody);
    }

    [Fact]
    public async Task SendOtpEmailAsync_returns_false_when_response_is_error_status()
    {
        var capture = new CapturingHandler(HttpStatusCode.InternalServerError);

        using var httpClient = new HttpClient(capture)
        {
            BaseAddress = new Uri("http://notification.local/")
        };

        var client = new EmailNotificationClient(httpClient, NullLogger<EmailNotificationClient>.Instance);

        var sent = await client.SendOtpEmailAsync("buyer@nexus.local", "Buyer Test", "654321", 5);

        Assert.False(sent);
    }

    [Fact]
    public async Task SendOtpEmailAsync_returns_false_when_exception_is_thrown()
    {
        var capture = new CapturingHandler(throwException: true);

        using var httpClient = new HttpClient(capture)
        {
            BaseAddress = new Uri("http://notification.local/")
        };

        var client = new EmailNotificationClient(httpClient, NullLogger<EmailNotificationClient>.Instance);

        var sent = await client.SendOtpEmailAsync("buyer@nexus.local", "Buyer Test", "654321", 5);

        Assert.False(sent);
    }

    [Fact]
    public async Task SendSellerApprovedEmailAsync_returns_false_when_response_is_error_status()
    {
        var capture = new CapturingHandler(HttpStatusCode.BadRequest);

        using var httpClient = new HttpClient(capture)
        {
            BaseAddress = new Uri("http://notification.local/")
        };

        var client = new EmailNotificationClient(httpClient, NullLogger<EmailNotificationClient>.Instance);

        var sent = await client.SendSellerApprovedEmailAsync("shop@nexus.local", "Seller Test", "Nexus Shop");

        Assert.False(sent);
    }

    [Fact]
    public async Task SendSellerRejectedEmailAsync_returns_false_when_response_is_error_status()
    {
        var capture = new CapturingHandler(HttpStatusCode.ServiceUnavailable);

        using var httpClient = new HttpClient(capture)
        {
            BaseAddress = new Uri("http://notification.local/")
        };

        var client = new EmailNotificationClient(httpClient, NullLogger<EmailNotificationClient>.Instance);

        var sent = await client.SendSellerRejectedEmailAsync("shop@nexus.local", "Seller Test", "Nexus Shop", "Reason");

        Assert.False(sent);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly bool _throwException;

        public CapturingHandler(HttpStatusCode statusCode = HttpStatusCode.OK, bool throwException = false)
        {
            _statusCode = statusCode;
            _throwException = throwException;
        }

        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (_throwException)
            {
                throw new HttpRequestException("Simulated connection error.");
            }

            LastRequest = request;
            LastBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(_statusCode);
        }
    }
}
