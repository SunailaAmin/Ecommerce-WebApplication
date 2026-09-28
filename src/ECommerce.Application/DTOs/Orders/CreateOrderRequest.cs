using System.ComponentModel.DataAnnotations;

namespace ECommerce.Application.DTOs.Orders;

public class CreateOrderRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "ShippingAddressId must be greater than 0.")]
    public int ShippingAddressId { get; set; }
}
