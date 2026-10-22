namespace User.Domain.Enums;

public enum PenaltyViolationType
{
    PaymentTimeout = 0,
    AuctionNonPayment = 1,
    CancellationWithoutReason = 2,
    Fraud = 3,
    Other = 4
}
