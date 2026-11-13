namespace ECommerce.Orders.Application.DTOs.Orders;

public sealed record OrderItemResponse(
    int Id,
    int ProductId,
    string ProductName,
    string Sku,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice);
