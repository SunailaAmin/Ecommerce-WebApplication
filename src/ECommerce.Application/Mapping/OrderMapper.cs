using ECommerce.Application.DTOs.Orders;
using ECommerce.Domain.Entities;

namespace ECommerce.Application.Mapping;

/// <summary>
/// Shared order mapping, used by order queries and by checkout so an order is never
/// shaped two different ways.
/// </summary>
public static class OrderMapper
{
    public static OrderResponse ToResponse(this Order order) => new(
        order.Id,
        order.OrderNumber,
        order.UserId,
        $"{order.User.FirstName} {order.User.LastName}".Trim(),
        order.User.Email,
        order.TotalAmount,
        order.Status,
        order.ShippingAddress.ToResponse(),
        order.Items.Select(ToItemResponse).ToList(),
        order.CreatedAt,
        order.UpdatedAt);

    private static OrderItemResponse ToItemResponse(OrderItem item) => new(
        item.Id,
        item.ProductId,
        item.Product.Name,
        item.Product.Sku,
        item.Quantity,
        item.UnitPrice,
        item.TotalPrice);
}
