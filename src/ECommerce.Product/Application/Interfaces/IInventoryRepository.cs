using ECommerce.Products.Application.DTOs;
using ECommerce.Products.Application.DTOs.Inventory;
using ECommerce.Products.Domain.Entities;
using ECommerce.Shared.DTOs;

namespace ECommerce.Products.Application.Interfaces;

public interface IInventoryRepository
{
    Task<PagedResult<Inventory>> GetPagedAsync(InventoryQuery query, CancellationToken cancellationToken);

    Task<Inventory?> GetByProductIdAsync(int productId, CancellationToken cancellationToken);

    void Add(Inventory inventory);

    void Update(Inventory inventory);
}
