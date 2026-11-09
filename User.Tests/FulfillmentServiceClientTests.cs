namespace User.Tests;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using User.Infrastructure.Services.External;
using Xunit;

public sealed class FulfillmentServiceClientTests
{
    [Fact]
    public async Task CreateWarehouseAsync_sends_effectiveSellerId_as_both_seller_id_and_user_id()
    {
        var capture = new CapturingHandler(HttpStatusCode.OK);
        using var httpClient = new HttpClient(capture)
        {
            BaseAddress = new Uri("http://fulfillment.local/")
        };

        var client = new FulfillmentServiceClient(httpClient, NullLogger<FulfillmentServiceClient>.Instance);

        var sellerProfileId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var result = await client.CreateWarehouseAsync(
            sellerId: sellerProfileId,
            userId: userId,
            shopName: "TDD Shop",
            contactName: "Nguyen Van A",
            contactPhone: "0912345678",
            pickupAddress: "123 Le Loi",
            provinceId: 1,
            districtId: 2,
            wardCode: "001");

        Assert.True(result);
        Assert.NotNull(capture.LastRequest);
        Assert.Equal("/api/v1/warehouses/initialize", capture.LastRequest!.RequestUri!.AbsolutePath);

        using var doc = JsonDocument.Parse(capture.LastBody!);
        var root = doc.RootElement;
        Assert.Equal(userId.ToString(), root.GetProperty("seller_id").GetString());
        Assert.Equal(userId.ToString(), root.GetProperty("user_id").GetString());
        Assert.Equal("TDD Shop", root.GetProperty("shop_name").GetString());
    }

    [Fact]
    public async Task CreateWarehouseAsync_falls_back_to_internal_route_when_primary_route_returns_404()
    {
        var responseMap = new Queue<HttpResponseMessage>();
        responseMap.Enqueue(new HttpResponseMessage(HttpStatusCode.NotFound));
        responseMap.Enqueue(new HttpResponseMessage(HttpStatusCode.OK));

        var capture = new MultiResponseHandler(responseMap);
        using var httpClient = new HttpClient(capture)
        {
            BaseAddress = new Uri("http://fulfillment.local/")
        };

        var client = new FulfillmentServiceClient(httpClient, NullLogger<FulfillmentServiceClient>.Instance);

        var sellerProfileId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var result = await client.CreateWarehouseAsync(
            sellerId: sellerProfileId,
            userId: userId,
            shopName: "Fallback Shop",
            contactName: "Nguyen Van B",
            contactPhone: "0987654321",
            pickupAddress: "456 Tran Phu",
            provinceId: 10,
            districtId: 20,
            wardCode: "002");

        Assert.True(result);
        Assert.Equal(2, capture.Requests.Count);
        Assert.Equal("/api/v1/warehouses/initialize", capture.Requests[0].RequestUri!.AbsolutePath);
        Assert.Equal("/api/v1/internal/warehouses", capture.Requests[1].RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task CreateWarehouseAsync_does_not_invoke_fallback_when_primary_route_succeeds()
    {
        var responseMap = new Queue<HttpResponseMessage>();
        responseMap.Enqueue(new HttpResponseMessage(HttpStatusCode.Created));

        var capture = new MultiResponseHandler(responseMap);
        using var httpClient = new HttpClient(capture)
        {
            BaseAddress = new Uri("http://fulfillment.local/")
        };

        var client = new FulfillmentServiceClient(httpClient, NullLogger<FulfillmentServiceClient>.Instance);

        var result = await client.CreateWarehouseAsync(
            sellerId: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            shopName: "Primary Shop",
            contactName: "Nguyen Van C",
            contactPhone: "0900000000",
            pickupAddress: "789 Nguyen Hue",
            provinceId: 1,
            districtId: 1,
            wardCode: "003");

        Assert.True(result);
        Assert.Single(capture.Requests);
        Assert.Equal("/api/v1/warehouses/initialize", capture.Requests[0].RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task UpdateWarehouseAsync_sends_put_to_seller_warehouse_route()
    {
        var capture = new CapturingHandler(HttpStatusCode.OK);
        using var httpClient = new HttpClient(capture)
        {
            BaseAddress = new Uri("http://fulfillment.local/")
        };

        var client = new FulfillmentServiceClient(httpClient, NullLogger<FulfillmentServiceClient>.Instance);
        var targetSellerId = Guid.NewGuid();

        var result = await client.UpdateWarehouseAsync(
            sellerId: targetSellerId,
            shopName: "Updated Shop",
            contactPhone: "0911111111",
            pickupAddress: "100 Dong Khoi",
            provinceId: 1,
            districtId: 3,
            wardCode: "004");

        Assert.True(result);
        Assert.NotNull(capture.LastRequest);
        Assert.Equal(HttpMethod.Put, capture.LastRequest!.Method);
        Assert.Equal($"/api/v1/warehouses/seller/{targetSellerId}", capture.LastRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task DeleteWarehouseBySellerIdAsync_sends_delete_to_seller_warehouse_route()
    {
        var capture = new CapturingHandler(HttpStatusCode.OK);
        using var httpClient = new HttpClient(capture)
        {
            BaseAddress = new Uri("http://fulfillment.local/")
        };

        var client = new FulfillmentServiceClient(httpClient, NullLogger<FulfillmentServiceClient>.Instance);
        var targetSellerId = Guid.NewGuid();

        var result = await client.DeleteWarehouseBySellerIdAsync(targetSellerId);

        Assert.True(result);
        Assert.NotNull(capture.LastRequest);
        Assert.Equal(HttpMethod.Delete, capture.LastRequest!.Method);
        Assert.Equal($"/api/v1/warehouses/seller/{targetSellerId}", capture.LastRequest.RequestUri!.AbsolutePath);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;

        public CapturingHandler(HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _statusCode = statusCode;
        }

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

            return new HttpResponseMessage(_statusCode);
        }
    }

    private sealed class MultiResponseHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;
        public List<HttpRequestMessage> Requests { get; } = new();

        public MultiResponseHandler(Queue<HttpResponseMessage> responses)
        {
            _responses = responses;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (_responses.Count > 0)
            {
                return Task.FromResult(_responses.Dequeue());
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
