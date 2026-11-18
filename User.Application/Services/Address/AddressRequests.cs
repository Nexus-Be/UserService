using System.ComponentModel.DataAnnotations;

namespace User.Application.Services.Address;

public sealed record CreateAddressRequest(
    [Required(ErrorMessage = "Recipient name is required.")]
    [StringLength(150, ErrorMessage = "Recipient name must not exceed 150 characters.")]
    string RecipientName,
    [Required(ErrorMessage = "Phone number is required.")]
    [StringLength(30, ErrorMessage = "Phone number must not exceed 30 characters.")]
    string Phone,
    [StringLength(60, ErrorMessage = "Label must not exceed 60 characters.")]
    string? Label,
    [Range(1, int.MaxValue, ErrorMessage = "Province ID must be positive.")]
    int ProvinceId,
    [Required(ErrorMessage = "Province name is required.")]
    [StringLength(150, ErrorMessage = "Province name must not exceed 150 characters.")]
    string ProvinceName,
    [Range(1, int.MaxValue, ErrorMessage = "District ID must be positive.")]
    int DistrictId,
    [Required(ErrorMessage = "District name is required.")]
    [StringLength(150, ErrorMessage = "District name must not exceed 150 characters.")]
    string DistrictName,
    [Required(ErrorMessage = "Ward code is required.")]
    [StringLength(50, ErrorMessage = "Ward code must not exceed 50 characters.")]
    string WardCode,
    [Required(ErrorMessage = "Ward name is required.")]
    [StringLength(150, ErrorMessage = "Ward name must not exceed 150 characters.")]
    string WardName,
    [Required(ErrorMessage = "Address line is required.")]
    [StringLength(500, ErrorMessage = "Address line must not exceed 500 characters.")]
    string AddressLine,
    bool IsDefault = false);

public sealed record UpdateAddressRequest(
    [Required(ErrorMessage = "Recipient name is required.")]
    [StringLength(150, ErrorMessage = "Recipient name must not exceed 150 characters.")]
    string RecipientName,
    [Required(ErrorMessage = "Phone number is required.")]
    [StringLength(30, ErrorMessage = "Phone number must not exceed 30 characters.")]
    string Phone,
    [StringLength(60, ErrorMessage = "Label must not exceed 60 characters.")]
    string? Label,
    [Range(1, int.MaxValue, ErrorMessage = "Province ID must be positive.")]
    int ProvinceId,
    [Required(ErrorMessage = "Province name is required.")]
    [StringLength(150, ErrorMessage = "Province name must not exceed 150 characters.")]
    string ProvinceName,
    [Range(1, int.MaxValue, ErrorMessage = "District ID must be positive.")]
    int DistrictId,
    [Required(ErrorMessage = "District name is required.")]
    [StringLength(150, ErrorMessage = "District name must not exceed 150 characters.")]
    string DistrictName,
    [Required(ErrorMessage = "Ward code is required.")]
    [StringLength(50, ErrorMessage = "Ward code must not exceed 50 characters.")]
    string WardCode,
    [Required(ErrorMessage = "Ward name is required.")]
    [StringLength(150, ErrorMessage = "Ward name must not exceed 150 characters.")]
    string WardName,
    [Required(ErrorMessage = "Address line is required.")]
    [StringLength(500, ErrorMessage = "Address line must not exceed 500 characters.")]
    string AddressLine,
    bool IsDefault = false);
