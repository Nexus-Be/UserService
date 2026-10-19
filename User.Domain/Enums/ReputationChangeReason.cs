namespace User.Domain.Enums;

public enum ReputationChangeReason
{
    PositiveReview = 0,
    NegativeReview = 1,
    OrderCompleted = 2,
    AuctionPaymentTimeout = 3,
    DisputeCompensation = 4,
    ManualAdjustment = 5,
    ViolationPenalty = 6
}
