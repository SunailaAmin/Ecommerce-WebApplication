using System.ComponentModel.DataAnnotations;
using ECommerce.Orders.Domain.Enums;

namespace ECommerce.Orders.Application.DTOs.Orders;

public class UpdateOrderStatusRequest
{
    [EnumDataType(typeof(OrderStatus), ErrorMessage = "Status must be a valid order status.")]
    public OrderStatus Status { get; set; }
}
