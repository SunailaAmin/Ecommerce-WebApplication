using ECommerce.Orders.Domain.Entities;

namespace ECommerce.Orders.Application.Interfaces;

public interface ICartRepository
{
    /// <summary>
    /// Returns the user's cart with items, products and inventory loaded. Tracked for
    /// update, so item quantities and stock can be changed in the same save without an
    /// explicit Update call.
    /// </summary>
    Task<Cart?> GetByUserIdAsync(int userId, CancellationToken cancellationToken);

    void Add(Cart cart);
}
