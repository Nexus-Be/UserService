namespace User.Tests;

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using User.Application.Interfaces;
using User.Infrastructure.Services.External;
using Xunit;

public sealed class CommerceOrderClientTests
{
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(respond(request));
        }
    }

    private static CommerceOrderClient Client(Handler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://commerce.test/") }, NullLogger<CommerceOrderClient>.Instance);

    [Fact]
    public async Task AKnownOrder_IsReadFromTheCamelCaseAnswer_AndTheRightUrlIsCalled()
    {
        var id = Guid.NewGuid();
        var buyer = Guid.NewGuid();
        var seller = Guid.NewGuid();
        var handler = new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$"""{"referenceType":"Order","referenceId":"{{id}}","status":"Completed","buyerId":"{{buyer}}","sellerId":"{{seller}}","completedAt":"2026-11-15T08:00:00+00:00"}""")
        });

        var result = await Client(handler).GetReviewReferenceAsync("Order", id);

        Assert.NotNull(result);
        Assert.Equal("Completed", result!.Status);
        Assert.Equal(buyer, result.BuyerId);
        Assert.Equal(seller, result.SellerId);
        Assert.Equal(new DateTimeOffset(2026, 11, 15, 8, 0, 0, TimeSpan.Zero), result.CompletedAt);
        Assert.Equal($"/api/v1/internal/review-eligibility", handler.Request!.RequestUri!.AbsolutePath);
        Assert.Equal($"?referenceType=Order&referenceId={id}", handler.Request.RequestUri.Query);
    }

    [Fact]
    public async Task AnUnknownOrder_IsNull()
    {
        var handler = new Handler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        Assert.Null(await Client(handler).GetReviewReferenceAsync("Order", Guid.NewGuid()));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task AnyOtherAnswer_MeansCommerceCannotTell_NotThatTheOrderIsMissing(HttpStatusCode status)
    {
        var handler = new Handler(_ => new HttpResponseMessage(status));

        await Assert.ThrowsAsync<CommerceUnavailableException>(() => Client(handler).GetReviewReferenceAsync("Order", Guid.NewGuid()));
    }

    [Fact]
    public async Task ANetworkFailure_OrATimeout_MeansCommerceCannotTell()
    {
        var down = new Handler(_ => throw new HttpRequestException("connection refused"));
        var slow = new Handler(_ => throw new TaskCanceledException("timeout"));
        var garbage = new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("not json") });

        await Assert.ThrowsAsync<CommerceUnavailableException>(() => Client(down).GetReviewReferenceAsync("Order", Guid.NewGuid()));
        await Assert.ThrowsAsync<CommerceUnavailableException>(() => Client(slow).GetReviewReferenceAsync("Order", Guid.NewGuid()));
        await Assert.ThrowsAsync<CommerceUnavailableException>(() => Client(garbage).GetReviewReferenceAsync("Order", Guid.NewGuid()));
    }
}
