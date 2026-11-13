namespace ECommerce.Orders.Application.DTOs.Cart;

public sealed record CartResponse(
    int Id,
    IReadOnlyList<CartItemResponse> Items,
    decimal Subtotal,
    int TotalQuantity);
