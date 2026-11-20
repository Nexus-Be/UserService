namespace User.Domain.Enums;

public enum ReputationChangeReason
{
    PositiveReview = 0,
    NegativeReview = 1,
    OrderCompleted = 2,
    AuctionPaymentTimeout = 3,
    DisputeCompensation = 4,
    ManualAdjustment = 5,
    ViolationPenalty = 6,
    SellerCancellation = 7,
    ReportPenalty = 8,
    /// <summary>The buyer refused or could not be reached for a delivery and the parcel came back. Not applied automatically until the carrier reason codes are agreed.</summary>
    BuyerRefusedDelivery = 9
}
