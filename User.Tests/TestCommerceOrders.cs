namespace User.Tests;

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using User.Application.Interfaces;

/// <summary>Stands in for CommerceService in review tests: it knows only the orders a test registers.</summary>
public sealed class FakeCommerceOrderClient : ICommerceOrderClient
{
    private readonly Dictionary<(string Type, Guid Id), OrderReviewReference> _orders = new();

    /// <summary>When set, every lookup fails as if Commerce were down.</summary>
    public bool Unavailable { get; set; }

    public int Calls { get; private set; }

    public OrderReviewReference Add(
        string referenceType, Guid referenceId, Guid buyerId, Guid sellerId, string status = "Completed", DateTimeOffset? completedAt = null)
    {
        var reference = new OrderReviewReference(referenceType, referenceId, status, buyerId, sellerId, completedAt);
        _orders[(referenceType, referenceId)] = reference;
        return reference;
    }

    /// <summary>Registers a completed order (finished a day ago) and returns its reference ID as the text a client would send.</summary>
    public string AddCompleted(Guid buyerId, Guid sellerId, string referenceType = "Order", DateTimeOffset? completedAt = null)
    {
        var id = Guid.NewGuid();
        Add(referenceType, id, buyerId, sellerId, "Completed", completedAt ?? DateTimeOffset.UtcNow.AddDays(-1));
        return id.ToString();
    }

    public Task<OrderReviewReference?> GetReviewReferenceAsync(
        string referenceType, Guid referenceId, CancellationToken cancellationToken = default)
    {
        Calls++;
        if (Unavailable)
            throw new CommerceUnavailableException("Commerce is down.");
        return Task.FromResult(_orders.TryGetValue((referenceType, referenceId), out var found) ? found : null);
    }
}

public static class TestOrders
{
    /// <summary>A stable reference ID for a readable test name such as "ORD-001".</summary>
    public static string Ref(string name) => new Guid(MD5.HashData(Encoding.UTF8.GetBytes(name))).ToString();
}
