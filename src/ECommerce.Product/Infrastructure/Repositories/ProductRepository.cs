using ECommerce.Products.Application.DTOs;
using ECommerce.Products.Application.DTOs.Products;
using ECommerce.Products.Application.Interfaces;
using ECommerce.Products.Domain.Entities;
using ECommerce.Products.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using ECommerce.Shared.DTOs;

namespace ECommerce.Products.Infrastructure.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly ECommerceDbContext _context;

    public ProductRepository(ECommerceDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<Product>> GetPagedAsync(ProductQuery query, CancellationToken cancellationToken)
    {
        var products = _context.Products
            .AsNoTracking()
            .Include(x => x.Category)
            .AsQueryable();

        if (!query.IncludeInactive)
        {
            products = products.Where(x => x.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            products = products.Where(x => x.Name.Contains(search) || x.Sku.Contains(search));
        }

        if (query.CategoryId.HasValue)
        {
            products = products.Where(x => x.CategoryId == query.CategoryId.Value);
        }

        var totalCount = await products.CountAsync(cancellationToken);

        var sorted = Sort(products, query.SortBy, query.SortDescending);

        var items = await sorted
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Product>(items, totalCount, query.Page, query.PageSize);
    }

    public Task<Product?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        return _context.Products
            .Include(x => x.Category)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<bool> ExistsAsync(int id, CancellationToken cancellationToken)
    {
        return _context.Products.AnyAsync(x => x.Id == id, cancellationToken);
    }

    public Task<bool> SkuExistsAsync(string sku, int? excludeProductId, CancellationToken cancellationToken)
    {
        return _context.Products
            .AnyAsync(x => x.Sku == sku && (!excludeProductId.HasValue || x.Id != excludeProductId.Value), cancellationToken);
    }

    public void Add(Product product)
    {
        _context.Products.Add(product);
    }

    public void Update(Product product)
    {
        _context.Products.Update(product);
    }

    private static IQueryable<Product> Sort(IQueryable<Product> products, string? sortBy, bool descending)
    {
        return sortBy?.ToLowerInvariant() switch
        {
            "price" => descending
                ? products.OrderByDescending(x => x.Price)
                : products.OrderBy(x => x.Price),
            "createdat" => descending
                ? products.OrderByDescending(x => x.CreatedAt)
                : products.OrderBy(x => x.CreatedAt),
            _ => descending
                ? products.OrderByDescending(x => x.Name)
                : products.OrderBy(x => x.Name)
        };
    }
}
