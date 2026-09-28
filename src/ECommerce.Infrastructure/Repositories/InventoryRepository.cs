using ECommerce.Application.DTOs;
using ECommerce.Application.DTOs.Inventory;
using ECommerce.Application.Interfaces;
using ECommerce.Domain.Entities;
using ECommerce.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Infrastructure.Repositories;

public class InventoryRepository : IInventoryRepository
{
    private readonly ECommerceDbContext _context;

    public InventoryRepository(ECommerceDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<Inventory>> GetPagedAsync(InventoryQuery query, CancellationToken cancellationToken)
    {
        var inventory = _context.Inventories
            .AsNoTracking()
            .Include(x => x.Product)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            inventory = inventory.Where(x => x.Product.Name.Contains(search) || x.Product.Sku.Contains(search));
        }

        if (query.LowStockAtOrBelow.HasValue)
        {
            inventory = inventory.Where(x => x.Quantity <= query.LowStockAtOrBelow.Value);
        }

        var totalCount = await inventory.CountAsync(cancellationToken);

        var items = await inventory
            .OrderBy(x => x.Product.Name)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Inventory>(items, totalCount, query.Page, query.PageSize);
    }

    public Task<Inventory?> GetByProductIdAsync(int productId, CancellationToken cancellationToken)
    {
        return _context.Inventories
            .Include(x => x.Product)
            .FirstOrDefaultAsync(x => x.ProductId == productId, cancellationToken);
    }

    public void Add(Inventory inventory)
    {
        _context.Inventories.Add(inventory);
    }

    public void Update(Inventory inventory)
    {
        _context.Inventories.Update(inventory);
    }
}
