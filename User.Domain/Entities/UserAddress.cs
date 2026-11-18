using System;
using User.Domain.Base;

namespace User.Domain.Entities;

/// <summary>
/// Saved shipping address belonging to a user, including GHN master data identifiers.
/// </summary>
public class UserAddress : AuditableEntity<Guid>
{
    public Guid UserId { get; set; }
    public required string RecipientName { get; set; }
    public required string Phone { get; set; }
    public string? Label { get; set; }

    public int ProvinceId { get; set; }
    public required string ProvinceName { get; set; }
    public int DistrictId { get; set; }
    public required string DistrictName { get; set; }
    public required string WardCode { get; set; }
    public required string WardName { get; set; }
    public required string AddressLine { get; set; }

    public bool IsDefault { get; set; }

    public User? User { get; set; }
}
