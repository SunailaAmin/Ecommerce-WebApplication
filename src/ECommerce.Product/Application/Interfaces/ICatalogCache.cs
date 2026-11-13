using ECommerce.Products.Application.DTOs;
using ECommerce.Products.Application.DTOs.Categories;
using ECommerce.Products.Application.DTOs.Products;
using ECommerce.Shared.DTOs;

namespace ECommerce.Products.Application.Interfaces;

/// <summary>
/// Read-through cache for the public catalog. Only shared, non-personal data belongs here:
/// carts, orders and profile data are never cached.
/// </summary>
public interface ICatalogCache
{
    Task<PagedResult<ProductResponse>> GetProductsAsync(
        string cacheKey,
        Func<CancellationToken, Task<PagedResult<ProductResponse>>> factory,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns the cached product, or runs the factory. A factory that throws for a missing
    /// product propagates the exception and caches nothing, so a product created later is
    /// picked up straight away.
    /// </summary>
    Task<ProductResponse> GetProductAsync(
        int productId,
        Func<CancellationToken, Task<ProductResponse>> factory,
        CancellationToken cancellationToken);

    Task<List<CategoryResponse>> GetCategoriesAsync(
        bool includeInactive,
        Func<CancellationToken, Task<List<CategoryResponse>>> factory,
        CancellationToken cancellationToken);

    /// <summary>
    /// Drops one product. A miss is not cached, so a product created later is picked up
    /// straight away.
    /// </summary>
    void InvalidateProduct(int productId);

    /// <summary>
    /// Drops every cached product list, because a list can contain any product.
    /// </summary>
    void InvalidateProductLists();

    /// <summary>
    /// Drops the cached categories. Product responses carry the category name, so the
    /// product lists have to go as well.
    /// </summary>
    void InvalidateCategories();
}
