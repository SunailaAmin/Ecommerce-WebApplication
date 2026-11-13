namespace ECommerce.Products.Application.DTOs.Inventory;

/// <summary>
/// Everything the Order Service needs about one product in a single round trip: display
/// fields for cart and order lines, IsActive for the sellability rule, Quantity for the
/// stock check. Exposed only on the internal snapshot endpoint.
/// </summary>
public sealed record InventorySnapshotResponse(
    int ProductId,
    string Name,
    string Sku,
    string? ImageUrl,
    decimal Price,
    bool IsActive,
    int Quantity,
    DateTime UpdatedAt);
