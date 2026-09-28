using ECommerce.Application.DTOs;
using ECommerce.Application.DTOs.Inventory;
using ECommerce.Domain.Entities;

namespace ECommerce.Application.Interfaces;

public interface IInventoryRepository
{
    Task<PagedResult<Inventory>> GetPagedAsync(InventoryQuery query, CancellationToken cancellationToken);

    Task<Inventory?> GetByProductIdAsync(int productId, CancellationToken cancellationToken);

    void Add(Inventory inventory);

    void Update(Inventory inventory);
}
