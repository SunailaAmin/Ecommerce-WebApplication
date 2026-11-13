using ECommerce.Products.Application.DTOs;
using ECommerce.Products.Application.DTOs.Products;
using ECommerce.Products.Domain.Entities;
using ECommerce.Shared.DTOs;

namespace ECommerce.Products.Application.Interfaces;

public interface IProductRepository
{
    Task<PagedResult<Product>> GetPagedAsync(ProductQuery query, CancellationToken cancellationToken);

    Task<Product?> GetByIdAsync(int id, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken);

    Task<bool> SkuExistsAsync(string sku, int? excludeProductId, CancellationToken cancellationToken);

    void Add(Product product);

    void Update(Product product);
}
