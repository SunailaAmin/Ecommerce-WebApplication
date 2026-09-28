using System.Net;
using System.Net.Http.Json;
using ECommerce.Application.DTOs;
using ECommerce.Application.DTOs.Products;
using ECommerce.IntegrationTests.Infrastructure;
using Xunit;

namespace ECommerce.IntegrationTests;

public class ProductsApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ProductsApiTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetProducts_ReturnsPagedEnvelope()
    {
        var client = await _factory.CreateAdminClientAsync();

        var response = await client.GetAsync("/api/products");
        var page = await response.Content.ReadFromJsonAsync<PagedResult<ProductResponse>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(page);
        Assert.NotNull(page!.Items);
        Assert.True(page.Page >= 1);
        Assert.True(page.PageSize > 0);
    }

    [Fact]
    public async Task GetProducts_SupportsPaging()
    {
        var client = await _factory.CreateAdminClientAsync();
        var category = await TestData.CreateCategoryAsync(client);

        for (var i = 0; i < 3; i++)
        {
            await TestData.CreateProductAsync(client, category.Id, name: TestData.UniqueName("Page"));
        }

        var page = await client.GetFromJsonAsync<PagedResult<ProductResponse>>(
            $"/api/products?categoryId={category.Id}&page=1&pageSize=2");

        Assert.Equal(3, page!.TotalCount);
        Assert.Equal(2, page.Items.Count);
        Assert.Equal(2, page.TotalPages);

        var second = await client.GetFromJsonAsync<PagedResult<ProductResponse>>(
            $"/api/products?categoryId={category.Id}&page=2&pageSize=2");

        Assert.Single(second!.Items);
    }

    [Fact]
    public async Task GetProducts_SearchMatchesNameOrSku()
    {
        var client = await _factory.CreateAdminClientAsync();
        var category = await TestData.CreateCategoryAsync(client);
        var nameMarker = TestData.UniqueName("ByName");
        var skuMarker = TestData.UniqueName("BySku");

        var byName = await TestData.CreateProductAsync(client, category.Id, name: $"{nameMarker} Phone", sku: "PLAIN-1");
        var bySku = await TestData.CreateProductAsync(client, category.Id, name: "Unrelated Widget", sku: $"SKU-{skuMarker}");

        var nameResults = await client.GetFromJsonAsync<PagedResult<ProductResponse>>($"/api/products?search={nameMarker}");
        var skuResults = await client.GetFromJsonAsync<PagedResult<ProductResponse>>($"/api/products?search=SKU-{skuMarker}");

        Assert.Equal(byName.Id, Assert.Single(nameResults!.Items).Id);
        Assert.Equal(bySku.Id, Assert.Single(skuResults!.Items).Id);
    }

    [Fact]
    public async Task GetProducts_FiltersByCategory()
    {
        var client = await _factory.CreateAdminClientAsync();
        var target = await TestData.CreateCategoryAsync(client);
        var other = await TestData.CreateCategoryAsync(client);

        await TestData.CreateProductAsync(client, target.Id, name: TestData.UniqueName("InTarget"));
        await TestData.CreateProductAsync(client, other.Id, name: TestData.UniqueName("InOther"));

        var page = await client.GetFromJsonAsync<PagedResult<ProductResponse>>($"/api/products?categoryId={target.Id}");

        Assert.NotEmpty(page!.Items);
        Assert.All(page.Items, p => Assert.Equal(target.Id, p.CategoryId));
    }

    [Fact]
    public async Task GetProducts_SortsByPrice()
    {
        var client = await _factory.CreateAdminClientAsync();
        var category = await TestData.CreateCategoryAsync(client);

        await TestData.CreateProductAsync(client, category.Id, price: 30m);
        await TestData.CreateProductAsync(client, category.Id, price: 10m);
        await TestData.CreateProductAsync(client, category.Id, price: 20m);

        var page = await client.GetFromJsonAsync<PagedResult<ProductResponse>>(
            $"/api/products?categoryId={category.Id}&sortBy=price&sortDescending=false&pageSize=50");

        var prices = page!.Items.Select(p => p.Price).ToList();

        Assert.Equal(prices.OrderBy(p => p).ToList(), prices);
        Assert.Equal(10m, prices.First());
    }

    [Fact]
    public async Task GetProduct_ReturnsProduct()
    {
        var client = await _factory.CreateAdminClientAsync();
        var category = await TestData.CreateCategoryAsync(client);
        var product = await TestData.CreateProductAsync(client, category.Id);

        var result = await client.GetFromJsonAsync<ProductResponse>($"/api/products/{product.Id}");

        Assert.Equal(product.Id, result!.Id);
        Assert.Equal(product.Sku, result.Sku);
        Assert.Equal(category.Id, result.CategoryId);
    }

    [Fact]
    public async Task GetProduct_ReturnsNotFound_ForUnknownId()
    {
        var client = await _factory.CreateAdminClientAsync();

        var response = await client.GetAsync("/api/products/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateProduct_ReturnsCreated_AndIsRetrievable()
    {
        var client = await _factory.CreateAdminClientAsync();
        var category = await TestData.CreateCategoryAsync(client);
        var sku = TestData.UniqueName("NewSku");

        var response = await client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            Name = "Created Product",
            Description = "Made by a test",
            Price = 499.99m,
            Sku = sku,
            CategoryId = category.Id,
            Quantity = 12
        });

        var created = await response.Content.ReadFromJsonAsync<ProductResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(created);
        Assert.Equal(sku, created!.Sku);
        Assert.Contains($"/api/products/{created.Id}", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task CreateProduct_ReturnsConflict_WhenSkuIsDuplicate()
    {
        var client = await _factory.CreateAdminClientAsync();
        var category = await TestData.CreateCategoryAsync(client);
        var sku = TestData.UniqueName("DupSku");

        await TestData.CreateProductAsync(client, category.Id, sku: sku);

        var response = await client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            Name = "Clashing Product",
            Price = 10m,
            Sku = sku,
            CategoryId = category.Id
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateProduct_ReturnsBadRequest_WhenCategoryDoesNotExist()
    {
        var client = await _factory.CreateAdminClientAsync();

        var response = await client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            Name = "Orphan Product",
            Price = 10m,
            Sku = TestData.UniqueName("Orphan"),
            CategoryId = 999999
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateProduct_ReturnsBadRequest_WhenRequestIsInvalid()
    {
        var client = await _factory.CreateAdminClientAsync();

        var response = await client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            Name = "",
            Price = 0m,
            Sku = "",
            CategoryId = 0
        });

        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Name", body);
        Assert.Contains("Sku", body);
    }

    [Fact]
    public async Task UpdateProduct_AppliesChanges()
    {
        var client = await _factory.CreateAdminClientAsync();
        var category = await TestData.CreateCategoryAsync(client);
        var product = await TestData.CreateProductAsync(client, category.Id);

        var response = await client.PutAsJsonAsync($"/api/products/{product.Id}", new UpdateProductRequest
        {
            Name = "Updated Name",
            Price = 777m,
            Sku = TestData.UniqueName("UpdSku"),
            CategoryId = category.Id,
            IsActive = true
        });

        var updated = await response.Content.ReadFromJsonAsync<ProductResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Updated Name", updated!.Name);
        Assert.Equal(777m, updated.Price);
    }

    [Fact]
    public async Task UpdateProduct_ReturnsNotFound_ForUnknownId()
    {
        var client = await _factory.CreateAdminClientAsync();
        var category = await TestData.CreateCategoryAsync(client);

        var response = await client.PutAsJsonAsync("/api/products/999999", new UpdateProductRequest
        {
            Name = "Nothing",
            Price = 1m,
            Sku = TestData.UniqueName("None"),
            CategoryId = category.Id
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteProduct_DeactivatesAndHidesFromListings()
    {
        var client = await _factory.CreateAdminClientAsync();
        var category = await TestData.CreateCategoryAsync(client);
        var product = await TestData.CreateProductAsync(client, category.Id);

        var response = await client.DeleteAsync($"/api/products/{product.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var listed = await client.GetFromJsonAsync<PagedResult<ProductResponse>>(
            $"/api/products?categoryId={category.Id}&includeInactive=false&pageSize=50");

        Assert.DoesNotContain(listed!.Items, p => p.Id == product.Id);

        var withInactive = await client.GetFromJsonAsync<PagedResult<ProductResponse>>(
            $"/api/products?categoryId={category.Id}&includeInactive=true&pageSize=50");

        Assert.Contains(withInactive!.Items, p => p.Id == product.Id && !p.IsActive);
    }

    [Fact]
    public async Task DeleteProduct_ReturnsNotFound_ForUnknownId()
    {
        var client = await _factory.CreateAdminClientAsync();

        var response = await client.DeleteAsync("/api/products/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
