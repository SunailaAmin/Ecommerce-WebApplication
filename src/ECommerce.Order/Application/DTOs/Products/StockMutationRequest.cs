namespace ECommerce.Orders.Application.DTOs.Products;

/// <summary>
/// Body of both POST /internal/inventory/reserve and POST /internal/inventory/release.
/// Releasing resends the exact reservation the reserve made, so the compensation cannot
/// drift from what it undoes.
/// </summary>
public sealed record StockMutationRequest(
    int OrderId,
    int ProductId,
    int Quantity);
