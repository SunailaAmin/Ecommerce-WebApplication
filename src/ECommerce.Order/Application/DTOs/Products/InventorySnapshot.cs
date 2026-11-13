namespace ECommerce.Orders.Application.DTOs.Products;

/// <summary>
/// The shape Product Service publishes on GET /internal/inventory/{productId}/snapshot.
/// This is the Order Service's own view of that contract, so a change on the Product side
/// fails here, in the consumer, rather than silently reshaping order responses.
/// </summary>
public sealed record InventorySnapshot(
    int ProductId,
    string Name,
    string Sku,
    string? ImageUrl,
    decimal Price,
    bool IsActive,
    int Quantity,
    DateTime UpdatedAt);
