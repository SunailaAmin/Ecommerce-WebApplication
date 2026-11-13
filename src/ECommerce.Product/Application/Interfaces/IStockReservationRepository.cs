using ECommerce.Products.Domain.Entities;

namespace ECommerce.Products.Application.Interfaces;

/// <summary>
/// The ledger that makes reserve and release idempotent per (OrderId, ProductId).
/// </summary>
public interface IStockReservationRepository
{
    Task<InventoryReservation?> FindAsync(int orderId, int productId, CancellationToken cancellationToken);

    void Add(InventoryReservation reservation);
}
