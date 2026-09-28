using System.ComponentModel.DataAnnotations;

namespace ECommerce.Application.DTOs.Payments;

/// <summary>
/// Turns the current cart into a paid order. The total is recalculated on the server, so
/// nothing about the amount is accepted from the client.
/// </summary>
public class CheckoutRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "ShippingAddressId must be greater than 0.")]
    public int ShippingAddressId { get; set; }

    [Required, StringLength(25, MinimumLength = 12, ErrorMessage = "CardNumber must be between 12 and 25 characters.")]
    public string CardNumber { get; set; } = string.Empty;

    [StringLength(100)]
    public string? CardHolderName { get; set; }
}
