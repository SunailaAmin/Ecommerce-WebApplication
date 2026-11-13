using ECommerce.Orders.Application.Interfaces;
using ECommerce.Orders.Domain.Entities;
using ECommerce.Orders.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Orders.Infrastructure.Repositories;

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
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
    }

    public void Add(Cart cart)
    {
        _context.Carts.Add(cart);
    }
}
