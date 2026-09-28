using ECommerce.Application.Interfaces;
using ECommerce.Domain.Entities;
using ECommerce.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Infrastructure.Repositories;

public class CartRepository : ICartRepository
{
    private readonly ECommerceDbContext _context;

    public CartRepository(ECommerceDbContext context)
    {
        _context = context;
    }

    public Task<Cart?> GetByUserIdAsync(int userId, CancellationToken cancellationToken)
    {
        return _context.Carts
            .Include(x => x.User)
            .Include(x => x.Items)
                .ThenInclude(x => x.Product)
                    .ThenInclude(x => x.Inventory)
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
    }

    public void Add(Cart cart)
    {
        _context.Carts.Add(cart);
    }
}
