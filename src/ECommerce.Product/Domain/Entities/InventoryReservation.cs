namespace ECommerce.Products.Domain.Entities;

/// <summary>
/// The idempotency ledger for stock an order has reserved. The unique (OrderId, ProductId)
/// pair is the whole point: a retried reserve finds this row instead of decrementing stock a
/// second time, and a retried release only restores units while ReleasedAt is still null.
/// This belongs to Product, because the quantity it guards is Product's to own.
/// </summary>
public class InventoryReservation
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReleasedAt { get; set; }
}
