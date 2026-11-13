namespace ECommerce.Products.Application.DTOs.Inventory;

/// <summary>
/// Applied is false when the call was an idempotent replay: reserve for an order already
/// reserved, or release for one already released. RemainingQuantity always reports the
/// stock standing after the call, whatever the outcome.
/// </summary>
public sealed record StockReservationResponse(
    int OrderId,
    int ProductId,
    int Quantity,
    bool Applied,
    int RemainingQuantity);
