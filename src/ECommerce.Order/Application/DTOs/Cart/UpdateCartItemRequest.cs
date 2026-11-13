using System.ComponentModel.DataAnnotations;

namespace ECommerce.Orders.Application.DTOs.Cart;

public class UpdateCartItemRequest
{
    [Range(1, 100, ErrorMessage = "Quantity must be between 1 and 100.")]
    public int Quantity { get; set; }
}
