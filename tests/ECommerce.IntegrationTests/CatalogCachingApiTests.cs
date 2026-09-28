using System.Net;
using System.Net.Http.Json;
using ECommerce.Application.DTOs;
using ECommerce.Application.DTOs.Categories;
using ECommerce.Application.DTOs.Products;
using ECommerce.IntegrationTests.Infrastructure;

namespace ECommerce.IntegrationTests;

/// <summary>
/// Exercises the catalog cache through the API, where the only thing that matters is that
/// a client never sees a stale product or category after an admin changes it.
/// </summary>
public class CatalogCachingApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public CatalogCachingApiTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<PagedResult<ProductResponse>> GetProductsAsync(string query = "")
    {
        return await ReadAsync<PagedResult<ProductResponse>>($"/api/products?{query}");
    }

    private async Task<T> ReadAsync<T>(string url)
    {
        var response = await _factory.CreateClient().GetAsync(url);

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    [Fact]
    public async Task ANewProductAppearsInTheList_WithoutWaitingForExpiration()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var category = await TestData.CreateCategoryAsync(admin);
        var name = TestData.UniqueName("Cached");

        // Populate the cache, then add a product and read the same page again.
        var before = await GetProductsAsync("pageSize=100");
        Assert.DoesNotContain(before.Items, x => x.Name == name);

        await TestData.CreateProductAsync(admin, category.Id, name: name, price: 42m);

        var after = await GetProductsAsync("pageSize=100");

        Assert.Contains(after.Items, x => x.Name == name && x.Price == 42m);
    }

    [Fact]
    public async Task AnUpdatedProductIsReflectedImmediately()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var category = await TestData.CreateCategoryAsync(admin);
        var product = await TestData.CreateProductAsync(admin, category.Id, price: 10m);

        var first = await ReadAsync<ProductResponse>($"/api/products/{product.Id}");
        Assert.Equal(10m, first.Price);

        var update = await admin.PutAsJsonAsync($"/api/products/{product.Id}", new UpdateProductRequest
        {
            Name = product.Name,
            Description = product.Description,
            Price = 99m,
            Sku = product.Sku,
            ImageUrl = product.ImageUrl,
            CategoryId = category.Id,
            IsActive = true
        });

        update.EnsureSuccessStatusCode();

        var second = await ReadAsync<ProductResponse>($"/api/products/{product.Id}");

        Assert.Equal(99m, second.Price);
    }

    [Fact]
    public async Task ADeactivatedProductDisappearsFromTheList()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var category = await TestData.CreateCategoryAsync(admin);
        var product = await TestData.CreateProductAsync(admin, category.Id);

        await GetProductsAsync("pageSize=100");

        var delete = await admin.DeleteAsync($"/api/products/{product.Id}");
        delete.EnsureSuccessStatusCode();

        var after = await GetProductsAsync("pageSize=100");

        Assert.DoesNotContain(after.Items, x => x.Id == product.Id);
    }

    [Fact]
    public async Task ANewCategoryAppearsWithoutWaitingForExpiration()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var name = TestData.UniqueName("CachedCat");

        var before = await ReadAsync<List<CategoryResponse>>("/api/categories");
        Assert.DoesNotContain(before, x => x.Name == name);

        var response = await admin.PostAsJsonAsync("/api/categories", new CreateCategoryRequest { Name = name });
        response.EnsureSuccessStatusCode();

        var after = await ReadAsync<List<CategoryResponse>>("/api/categories");

        Assert.Contains(after, x => x.Name == name);
    }

    [Fact]
    public async Task ARenamedCategoryIsReflectedInsideCachedProducts()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var originalName = TestData.UniqueName("Before");
        var renamedName = TestData.UniqueName("After");

        var createCategory = await admin.PostAsJsonAsync("/api/categories", new CreateCategoryRequest { Name = originalName });
        createCategory.EnsureSuccessStatusCode();

        var category = (await createCategory.Content.ReadFromJsonAsync<CategoryResponse>())!;
        var product = await TestData.CreateProductAsync(admin, category.Id);

        // Read the product while it carries the old category name.
        Assert.Equal(originalName, (await ReadAsync<ProductResponse>($"/api/products/{product.Id}")).CategoryName);

        var rename = await admin.PutAsJsonAsync($"/api/categories/{category.Id}", new UpdateCategoryRequest
        {
            Name = renamedName,
            IsActive = true
        });

        rename.EnsureSuccessStatusCode();

        // The product read is cached too, so a rename has to invalidate it or the old
        // name keeps showing.
        Assert.Equal(renamedName, (await ReadAsync<ProductResponse>($"/api/products/{product.Id}")).CategoryName);
    }

    [Fact]
    public async Task DifferentQueriesAreNotServedFromEachOthersCacheEntry()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var category = await TestData.CreateCategoryAsync(admin);
        var wanted = TestData.UniqueName("Needle");
        var other = TestData.UniqueName("Haystack");

        await TestData.CreateProductAsync(admin, category.Id, name: wanted);
        await TestData.CreateProductAsync(admin, category.Id, name: other);

        var search = await GetProductsAsync($"search={wanted}&pageSize=100");
        var firstPage = await GetProductsAsync("page=1&pageSize=1");
        var secondPage = await GetProductsAsync("page=2&pageSize=1");

        Assert.Contains(search.Items, x => x.Name == wanted);
        Assert.DoesNotContain(search.Items, x => x.Name == other);

        // A paged read must not hand back the first page for the second request.
        Assert.NotEqual(firstPage.Items.FirstOrDefault()?.Id, secondPage.Items.FirstOrDefault()?.Id);
    }

    [Fact]
    public async Task SearchingAndCategoryFilteringStayIndependent()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var categoryA = await TestData.CreateCategoryAsync(admin);
        var categoryB = await TestData.CreateCategoryAsync(admin);

        await TestData.CreateProductAsync(admin, categoryA.Id, name: TestData.UniqueName("Alpha"));
        await TestData.CreateProductAsync(admin, categoryB.Id, name: TestData.UniqueName("Beta"));

        var filtered = await GetProductsAsync($"categoryId={categoryA.Id}&pageSize=100");

        Assert.NotEmpty(filtered.Items);
        Assert.All(filtered.Items, x => Assert.Equal(categoryA.Id, x.CategoryId));
    }
}
