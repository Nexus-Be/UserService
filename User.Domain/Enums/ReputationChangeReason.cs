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
    /// <summary>The buyer refused to receive a delivery and the parcel came back. Only a carrier reason code the project owner has approved as a refusal counts; "could not be reached", a wrong address or an unknown reason never does. Commerce decides and sends it once per sub-order, and only while its approved table is configured.</summary>
    BuyerRefusedDelivery = 9
}
