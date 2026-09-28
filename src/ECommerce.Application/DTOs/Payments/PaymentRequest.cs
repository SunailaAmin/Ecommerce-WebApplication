using System.ComponentModel.DataAnnotations;

namespace ECommerce.Application.DTOs.Payments;

/// <summary>
/// Pays for an order that is still awaiting payment. The card number is only used to
/// decide the outcome and is never stored.
/// </summary>
public class PaymentRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "OrderId must be greater than 0.")]
    public int OrderId { get; set; }

    [Required, StringLength(25, MinimumLength = 12, ErrorMessage = "CardNumber must be between 12 and 25 characters.")]
    public string CardNumber { get; set; } = string.Empty;

    [StringLength(100)]
    public string? CardHolderName { get; set; }
}
