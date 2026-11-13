namespace ECommerce.Products.Application.DTOs.Inventory;

/// <summary>
/// Body of both /internal/inventory/reserve and /internal/inventory/release, so a
/// compensating release is byte-for-byte the same request as the reserve it undoes.
/// </summary>
public sealed record StockMutationRequest(
    int OrderId,
    int ProductId,
    int Quantity);
