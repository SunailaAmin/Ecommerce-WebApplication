using ECommerce.Orders.Application.DTOs.Products;

namespace ECommerce.Orders.Application.Interfaces;

/// <summary>
/// The Order Service's boundary to Product Service for inventory. Everything the order
/// flow needs about stock goes through here, so no part of Order ever reaches for
/// Product's database, entities or repositories directly.
/// </summary>
public interface IProductServiceClient
{
    Task<InventorySnapshot> GetInventorySnapshotAsync(int productId, CancellationToken cancellationToken);

    Task<StockReservationResult> ReserveStockAsync(StockMutationRequest request, CancellationToken cancellationToken);

    Task<StockReservationResult> ReleaseStockAsync(StockMutationRequest request, CancellationToken cancellationToken);
}
