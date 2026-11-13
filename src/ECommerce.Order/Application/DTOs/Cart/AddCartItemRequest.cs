using System.ComponentModel.DataAnnotations;

namespace ECommerce.Orders.Application.DTOs.Cart;

public class AddCartItemRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "ProductId must be greater than 0.")]
    public int ProductId { get; set; }

    [Range(1, 100, ErrorMessage = "Quantity must be between 1 and 100.")]
    public int Quantity { get; set; } = 1;
}
