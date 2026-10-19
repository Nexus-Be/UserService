using User.Domain.Base;
using User.Domain.Enums;

namespace User.Domain.Entities;

public class SellerProfile : BaseEntity<Guid>
{
    public required Guid UserId { get; set; }
    public required string ShopName { get; set; }
    public required string PickupAddress { get; set; }
    public required int ProvinceId { get; set; }
    public required int DistrictId { get; set; }
    public required string WardCode { get; set; }
    public required string Hotline { get; set; }
    public required SellerProfileStatus Status { get; set; } = SellerProfileStatus.Pending;
    public string? RejectionReason { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }

    // Navigation
    public User? User { get; set; }
}
