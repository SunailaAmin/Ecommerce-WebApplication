namespace ECommerce.Orders.Application.DTOs.Cart;

/// <summary>
/// A single line in the cart. Price is always read live from the product row, never
/// stored on the cart item, so the server stays the source of truth for pricing.
/// </summary>
public sealed record CartItemResponse(
    int Id,
    int ProductId,
    string ProductName,
    string Sku,
    string? ImageUrl,
    decimal UnitPrice,
    int Quantity,
    int AvailableStock,
    decimal LineTotal);
