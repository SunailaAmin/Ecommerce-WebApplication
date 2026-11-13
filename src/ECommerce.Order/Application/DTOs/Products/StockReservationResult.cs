namespace ECommerce.Orders.Application.DTOs.Products;

/// <summary>
/// Applied is false when Product treated the call as an idempotent replay, which is what
/// makes a retried reserve or a retried compensating release safe. RemainingQuantity is
/// stock after the call.
/// </summary>
public sealed record StockReservationResult(
    int OrderId,
    int ProductId,
    int Quantity,
    bool Applied,
    int RemainingQuantity);
