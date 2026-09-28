namespace ECommerce.Application.DTOs.Inventory;

public record InventoryResponse(
    int ProductId,
    string ProductName,
    string Sku,
    int Quantity,
    bool IsInStock,
    DateTime UpdatedAt);
