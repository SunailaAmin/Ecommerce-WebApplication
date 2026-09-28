using System.ComponentModel.DataAnnotations;

namespace ECommerce.Application.DTOs.Addresses;

public class CreateAddressRequest
{
    [Required, StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string AddressLine { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string City { get; set; } = string.Empty;

    [StringLength(100)]
    public string? State { get; set; }

    [Required, StringLength(20)]
    public string PostalCode { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string Country { get; set; } = string.Empty;

    [StringLength(30)]
    public string? PhoneNumber { get; set; }
}
