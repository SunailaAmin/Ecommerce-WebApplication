using ECommerce.Products.Application.Interfaces;
using ECommerce.Products.Domain.Entities;
using ECommerce.Products.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Products.Infrastructure.Repositories;

public class StockReservationRepository : IStockReservationRepository
{
    private readonly ECommerceDbContext _context;

    public StockReservationRepository(ECommerceDbContext context)
    {
        _context = context;
    }

    public Task<InventoryReservation?> FindAsync(int orderId, int productId, CancellationToken cancellationToken)
    {
        return _context.Reservations
            .FirstOrDefaultAsync(
                x => x.OrderId == orderId && x.ProductId == productId,
                cancellationToken);
    }

    public void Add(InventoryReservation reservation)
    {
        _context.Reservations.Add(reservation);
    }
}
