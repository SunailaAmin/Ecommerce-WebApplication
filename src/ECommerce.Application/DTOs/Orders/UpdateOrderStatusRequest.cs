using System.ComponentModel.DataAnnotations;
using ECommerce.Domain.Enums;

namespace ECommerce.Application.DTOs.Orders;

public class UpdateOrderStatusRequest
{
    [EnumDataType(typeof(OrderStatus), ErrorMessage = "Status must be a valid order status.")]
    public OrderStatus Status { get; set; }
}
