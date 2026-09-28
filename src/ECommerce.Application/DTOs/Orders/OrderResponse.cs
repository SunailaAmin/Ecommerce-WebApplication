using ECommerce.Application.DTOs.Addresses;
using ECommerce.Domain.Enums;

namespace ECommerce.Application.DTOs.Orders;

public sealed record OrderResponse(
    int Id,
    string OrderNumber,
    int UserId,
    string CustomerName,
    string CustomerEmail,
    decimal TotalAmount,
    OrderStatus Status,
    AddressResponse ShippingAddress,
    IReadOnlyList<OrderItemResponse> Items,
    DateTime CreatedAt,
    DateTime UpdatedAt);
