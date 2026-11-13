using ECommerce.Products.Application.Caching;
using ECommerce.Products.Application.DTOs;
using ECommerce.Products.Application.DTOs.Categories;
using ECommerce.Products.Application.DTOs.Products;
using ECommerce.Products.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ECommerce.Shared.DTOs;

namespace ECommerce.Products.Infrastructure.Caching;

/// <summary>
/// In-memory read-through cache for the catalog.
/// </summary>
/// <remarks>
/// Product lists and product details are cached under keys that embed a version number.
/// Bumping a version makes every previously cached entry for that kind unreachable in one
/// step, which is necessary because the key space is open-ended: paging, search, sorting and
/// category filters all produce different list keys, and the cached product ids cannot be
/// enumerated from <see cref="IMemoryCache"/>. The two versions are separate so that creating
/// one product does not throw away every cached product detail, while a category edit does.
/// Categories have a known key space and are removed directly.
/// </remarks>
public class CatalogCache : ICatalogCache
{
    private readonly IMemoryCache _cache;
    private readonly CatalogCacheOptions _options;
    private readonly ILogger<CatalogCache> _logger;

    private long _listVersion;
    private long _detailVersion;

    public CatalogCache(
        IMemoryCache cache,
        IOptions<CatalogCacheOptions> options,
        ILogger<CatalogCache> logger)
    {
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<PagedResult<ProductResponse>> GetProductsAsync(
        string cacheKey,
        Func<CancellationToken, Task<PagedResult<ProductResponse>>> factory,
        CancellationToken cancellationToken)
    {
        var key = $"products:v{Volatile.Read(ref _listVersion)}:{cacheKey}";

        if (_cache.TryGetValue<PagedResult<ProductResponse>>(key, out var cached))
        {
            return cached!;
        }

        var value = await factory(cancellationToken);

        _cache.Set(key, value, Minutes(_options.ProductListMinutes));

        return value;
    }

    public async Task<ProductResponse> GetProductAsync(
        int productId,
        Func<CancellationToken, Task<ProductResponse>> factory,
        CancellationToken cancellationToken)
    {
        var key = ProductKey(productId, Volatile.Read(ref _detailVersion));

        if (_cache.TryGetValue<ProductResponse>(key, out var cached))
        {
            return cached!;
        }

        // A factory that throws for a missing product propagates before anything is stored,
        // so a miss is never cached and a product created later is seen immediately.
        var value = await factory(cancellationToken);

        _cache.Set(key, value, Minutes(_options.ProductDetailMinutes));

        return value;
    }

    public async Task<List<CategoryResponse>> GetCategoriesAsync(
        bool includeInactive,
        Func<CancellationToken, Task<List<CategoryResponse>>> factory,
        CancellationToken cancellationToken)
    {
        var key = CategoryKey(includeInactive);

        if (_cache.TryGetValue<List<CategoryResponse>>(key, out var cached))
        {
            return cached!;
        }

        var value = await factory(cancellationToken);

        _cache.Set(key, value, Minutes(_options.CategoryMinutes));

        return value;
    }

    public void InvalidateProduct(int productId)
    {
        _cache.Remove(ProductKey(productId, Volatile.Read(ref _detailVersion)));
    }

    public void InvalidateProductLists()
    {
        Interlocked.Increment(ref _listVersion);
    }

    public void InvalidateCategories()
    {
        _cache.Remove(CategoryKey(includeInactive: false));
        _cache.Remove(CategoryKey(includeInactive: true));

        // A product response embeds the category name, so a renamed or deactivated category
        // would otherwise keep showing its old name inside cached product reads.
        InvalidateProductLists();
        Interlocked.Increment(ref _detailVersion);

        _logger.LogInformation("Catalog cache invalidated for categories, product lists and product details");
    }

    private static string ProductKey(int productId, long detailVersion) => $"product:v{detailVersion}:{productId}";

    private static string CategoryKey(bool includeInactive) => $"categories:{includeInactive}";

    private static MemoryCacheEntryOptions Minutes(int minutes) =>
        new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(minutes) };
}
