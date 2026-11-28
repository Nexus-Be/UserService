namespace User.Application.Interfaces;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>What CommerceService knows about an order a review refers to.</summary>
public sealed record OrderReviewReference(
    string ReferenceType,
    Guid ReferenceId,
    string Status,
    Guid BuyerId,
    Guid SellerId,
    DateTimeOffset? CompletedAt);

/// <summary>Raised when CommerceService cannot answer. A review is then refused (503) rather than accepted without proof.</summary>
public sealed class CommerceUnavailableException : Exception
{
    public CommerceUnavailableException(string message, Exception? innerException = null) : base(message, innerException)
    {
    }
}

/// <summary>Asks CommerceService who took part in an order and whether it finished. Only CommerceService knows.</summary>
public interface ICommerceOrderClient
{
    /// <summary>
    /// Looks up a sub-order (referenceType "Order", never the parent order) or an auction order ("Auction").
    /// Returns null when Commerce has no such order. Throws <see cref="CommerceUnavailableException"/> when it cannot tell.
    /// </summary>
    Task<OrderReviewReference?> GetReviewReferenceAsync(
        string referenceType, Guid referenceId, CancellationToken cancellationToken = default);
}
