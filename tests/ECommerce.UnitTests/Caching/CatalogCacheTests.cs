using ECommerce.Application.Caching;
using ECommerce.Application.DTOs;
using ECommerce.Application.DTOs.Categories;
using ECommerce.Application.DTOs.Products;
using ECommerce.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ECommerce.UnitTests.Caching;

public class CatalogCacheTests
{
    private readonly CatalogCache _cache;

    private int _factoryCalls;

    public CatalogCacheTests()
    {
        _cache = new CatalogCache(
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new CatalogCacheOptions
            {
                ProductListMinutes = 5,
                ProductDetailMinutes = 10,
                CategoryMinutes = 30
            }),
            NullLogger<CatalogCache>.Instance);
    }

    private static PagedResult<ProductResponse> ProductPage(string name) =>
        new([new ProductResponse(1, name, null, 10m, "SKU-1", null, 1, "Laptops", true, DateTime.UtcNow, DateTime.UtcNow)], 1, 1, 10);

    private Task<PagedResult<ProductResponse>> ProductFactory()
    {
        _factoryCalls++;

        return Task.FromResult(ProductPage($"Product {_factoryCalls}"));
    }

    private static ProductResponse Product(string name) =>
        new(1, name, null, 10m, "SKU-1", null, 1, "Laptops", true, DateTime.UtcNow, DateTime.UtcNow);

    [Fact]
    public async Task GetProductsAsync_ServesTheSecondReadFromCache()
    {
        var first = await _cache.GetProductsAsync("1|10", _ => ProductFactory(), CancellationToken.None);
        var second = await _cache.GetProductsAsync("1|10", _ => ProductFactory(), CancellationToken.None);

        Assert.Equal(1, _factoryCalls);
        Assert.Same(first, second);
    }

    [Fact]
    public async Task GetProductsAsync_KeepsDifferentQueriesApart()
    {
        await _cache.GetProductsAsync("1|10", _ => ProductFactory(), CancellationToken.None);
        await _cache.GetProductsAsync("2|10", _ => ProductFactory(), CancellationToken.None);

        // A different page or filter must not be answered from another query's entry.
        Assert.Equal(2, _factoryCalls);
    }

    [Fact]
    public async Task InvalidateProductLists_MakesEveryCachedListUnreachable()
    {
        await _cache.GetProductsAsync("1|10", _ => ProductFactory(), CancellationToken.None);
        await _cache.GetProductsAsync("2|10", _ => ProductFactory(), CancellationToken.None);

        _cache.InvalidateProductLists();

        await _cache.GetProductsAsync("1|10", _ => ProductFactory(), CancellationToken.None);
        await _cache.GetProductsAsync("2|10", _ => ProductFactory(), CancellationToken.None);

        Assert.Equal(4, _factoryCalls);
    }

    [Fact]
    public async Task InvalidateProductLists_LeavesProductDetailsAlone()
    {
        await _cache.GetProductAsync(7, _ => Task.FromResult(Product("Cached")), CancellationToken.None);

        _cache.InvalidateProductLists();

        var again = await _cache.GetProductAsync(7, _ => Task.FromResult(Product("Refetched")), CancellationToken.None);

        Assert.Equal("Cached", again.Name);
    }

    [Fact]
    public async Task GetProductAsync_ServesTheSecondReadFromCache()
    {
        var first = await _cache.GetProductAsync(7, _ => Task.FromResult(Product("Cached")), CancellationToken.None);
        var second = await _cache.GetProductAsync(7, _ => Task.FromResult(Product("Refetched")), CancellationToken.None);

        Assert.Equal("Cached", first.Name);
        Assert.Equal("Cached", second.Name);
    }

    [Fact]
    public async Task InvalidateProduct_OnlyDropsThatProduct()
    {
        await _cache.GetProductAsync(7, _ => Task.FromResult(Product("Cached")), CancellationToken.None);
        await _cache.GetProductAsync(8, _ => Task.FromResult(Product("Other")), CancellationToken.None);

        _cache.InvalidateProduct(7);

        var seven = await _cache.GetProductAsync(7, _ => Task.FromResult(Product("Refetched")), CancellationToken.None);
        var eight = await _cache.GetProductAsync(8, _ => Task.FromResult(Product("Refetched")), CancellationToken.None);

        Assert.Equal("Refetched", seven.Name);
        Assert.Equal("Other", eight.Name);
    }

    [Fact]
    public async Task GetProductAsync_PropagatesAMissWithoutCachingIt()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _cache.GetProductAsync(
                404,
                _ => throw new InvalidOperationException("not found"),
                CancellationToken.None));

        // Nothing was stored, so the next call reaches the factory again.
        var retried = await _cache.GetProductAsync(404, _ => Task.FromResult(Product("Now exists")), CancellationToken.None);

        Assert.Equal("Now exists", retried.Name);
    }

    [Fact]
    public async Task GetCategoriesAsync_ServesTheSecondReadFromCache()
    {
        var first = await _cache.GetCategoriesAsync(false, _ => Task.FromResult(new List<CategoryResponse>()), CancellationToken.None);
        var second = await _cache.GetCategoriesAsync(false, _ => Task.FromResult(new List<CategoryResponse>()), CancellationToken.None);

        Assert.Same(first, second);
    }

    [Fact]
    public async Task GetCategoriesAsync_KeepsIncludeInactiveApart()
    {
        var calls = 0;

        Task<List<CategoryResponse>> Factory(CancellationToken _)
        {
            calls++;

            return Task.FromResult(new List<CategoryResponse>());
        }

        await _cache.GetCategoriesAsync(false, Factory, CancellationToken.None);
        await _cache.GetCategoriesAsync(true, Factory, CancellationToken.None);

        // The admin view is a different set, so it is read separately.
        Assert.Equal(2, calls);

        await _cache.GetCategoriesAsync(false, Factory, CancellationToken.None);

        // Each variant is then served from its own cache entry.
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task InvalidateCategories_DropsCategoriesAndProductLists()
    {
        // Product responses carry the category name, so a category edit has to reach them.
        await _cache.GetCategoriesAsync(false, _ => Task.FromResult(new List<CategoryResponse>()), CancellationToken.None);
        await _cache.GetCategoriesAsync(true, _ => Task.FromResult(new List<CategoryResponse>()), CancellationToken.None);
        await _cache.GetProductsAsync("1|10", _ => ProductFactory(), CancellationToken.None);

        // Read again without invalidating: the factory must not run.
        await _cache.GetCategoriesAsync(false, _ => Task.FromResult(new List<CategoryResponse>()), CancellationToken.None);
        await _cache.GetCategoriesAsync(true, _ => Task.FromResult(new List<CategoryResponse>()), CancellationToken.None);
        await _cache.GetProductsAsync("1|10", _ => ProductFactory(), CancellationToken.None);

        Assert.Equal(1, _factoryCalls);

        _cache.InvalidateCategories();

        await _cache.GetCategoriesAsync(false, _ => Task.FromResult(new List<CategoryResponse>()), CancellationToken.None);
        await _cache.GetCategoriesAsync(true, _ => Task.FromResult(new List<CategoryResponse>()), CancellationToken.None);
        await _cache.GetProductsAsync("1|10", _ => ProductFactory(), CancellationToken.None);

        // The product list was read twice in total, which is what proves the category
        // invalidation reached it.
        Assert.Equal(2, _factoryCalls);
    }
}
