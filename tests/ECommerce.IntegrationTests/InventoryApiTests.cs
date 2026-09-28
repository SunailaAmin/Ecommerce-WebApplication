using System.Net;
using System.Net.Http.Json;
using ECommerce.Application.DTOs;
using ECommerce.Application.DTOs.Inventory;
using ECommerce.IntegrationTests.Infrastructure;
using Xunit;

namespace ECommerce.IntegrationTests;

public class InventoryApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public InventoryApiTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetInventory_ReturnsQuantityForProduct()
    {
        var client = await _factory.CreateAdminClientAsync();
        var category = await TestData.CreateCategoryAsync(client);
        var product = await TestData.CreateProductAsync(client, category.Id, quantity: 7);

        var inventory = await client.GetFromJsonAsync<InventoryResponse>($"/api/inventory/{product.Id}");

        Assert.Equal(product.Id, inventory!.ProductId);
        Assert.Equal(7, inventory.Quantity);
        Assert.True(inventory.IsInStock);
        Assert.Equal(product.Sku, inventory.Sku);
    }

    [Fact]
    public async Task GetInventory_ReturnsNotInStock_WhenQuantityIsZero()
    {
        var client = await _factory.CreateAdminClientAsync();
        var category = await TestData.CreateCategoryAsync(client);
        var product = await TestData.CreateProductAsync(client, category.Id, quantity: 0);

        var inventory = await client.GetFromJsonAsync<InventoryResponse>($"/api/inventory/{product.Id}");

        Assert.False(inventory!.IsInStock);
    }

    [Fact]
    public async Task GetInventory_ReturnsNotFound_ForUnknownProduct()
    {
        var client = await _factory.CreateAdminClientAsync();

        var response = await client.GetAsync("/api/inventory/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetInventory_ListsStockWithPaging()
    {
        var client = await _factory.CreateAdminClientAsync();
        var category = await TestData.CreateCategoryAsync(client);
        await TestData.CreateProductAsync(client, category.Id, name: TestData.UniqueName("ListInv"), quantity: 3);

        var page = await client.GetFromJsonAsync<PagedResult<InventoryResponse>>(
            $"/api/inventory?page=1&pageSize=1");

        Assert.Single(page!.Items);
        Assert.True(page.TotalCount >= 1);
    }

    [Fact]
    public async Task GetInventory_FiltersLowStock()
    {
        var client = await _factory.CreateAdminClientAsync();
        var category = await TestData.CreateCategoryAsync(client);

        var lowStock = await TestData.CreateProductAsync(client, category.Id, name: TestData.UniqueName("Low"), quantity: 1);
        await TestData.CreateProductAsync(client, category.Id, name: TestData.UniqueName("High"), quantity: 50);

        var page = await client.GetFromJsonAsync<PagedResult<InventoryResponse>>(
            $"/api/inventory?lowStockAtOrBelow=5&pageSize=100");

        Assert.Contains(page!.Items, i => i.ProductId == lowStock.Id);
        Assert.All(page.Items, i => Assert.True(i.Quantity <= 5));
    }

    [Fact]
    public async Task SetQuantity_ReplacesStockLevel()
    {
        var client = await _factory.CreateAdminClientAsync();
        var category = await TestData.CreateCategoryAsync(client);
        var product = await TestData.CreateProductAsync(client, category.Id, quantity: 5);

        var response = await client.PutAsJsonAsync($"/api/inventory/{product.Id}", new UpdateInventoryRequest
        {
            Quantity = 40
        });

        var inventory = await response.Content.ReadFromJsonAsync<InventoryResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(40, inventory!.Quantity);
    }

    [Fact]
    public async Task SetQuantity_ReturnsBadRequest_WhenQuantityIsNegative()
    {
        var client = await _factory.CreateAdminClientAsync();
        var category = await TestData.CreateCategoryAsync(client);
        var product = await TestData.CreateProductAsync(client, category.Id, quantity: 5);

        var response = await client.PutAsJsonAsync($"/api/inventory/{product.Id}", new UpdateInventoryRequest
        {
            Quantity = -1
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SetQuantity_ReturnsNotFound_ForUnknownProduct()
    {
        var client = await _factory.CreateAdminClientAsync();

        var response = await client.PutAsJsonAsync("/api/inventory/999999", new UpdateInventoryRequest { Quantity = 1 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AdjustQuantity_IncreasesStock()
    {
        var client = await _factory.CreateAdminClientAsync();
        var category = await TestData.CreateCategoryAsync(client);
        var product = await TestData.CreateProductAsync(client, category.Id, quantity: 10);

        var response = await client.PostAsJsonAsync($"/api/inventory/{product.Id}/adjust", new AdjustInventoryRequest
        {
            Delta = 15
        });

        var inventory = await response.Content.ReadFromJsonAsync<InventoryResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(25, inventory!.Quantity);
    }

    [Fact]
    public async Task AdjustQuantity_DecreasesStock()
    {
        var client = await _factory.CreateAdminClientAsync();
        var category = await TestData.CreateCategoryAsync(client);
        var product = await TestData.CreateProductAsync(client, category.Id, quantity: 10);

        var response = await client.PostAsJsonAsync($"/api/inventory/{product.Id}/adjust", new AdjustInventoryRequest
        {
            Delta = -4
        });

        var inventory = await response.Content.ReadFromJsonAsync<InventoryResponse>();

        Assert.Equal(6, inventory!.Quantity);
    }

    [Fact]
    public async Task AdjustQuantity_ReturnsBadRequest_WhenStockWouldGoNegative()
    {
        var client = await _factory.CreateAdminClientAsync();
        var category = await TestData.CreateCategoryAsync(client);
        var product = await TestData.CreateProductAsync(client, category.Id, quantity: 2);

        var response = await client.PostAsJsonAsync($"/api/inventory/{product.Id}/adjust", new AdjustInventoryRequest
        {
            Delta = -5
        });

        var after = await client.GetFromJsonAsync<InventoryResponse>($"/api/inventory/{product.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(2, after!.Quantity);
    }

    [Fact]
    public async Task AdjustQuantity_ReturnsBadRequest_WhenDeltaIsZero()
    {
        var client = await _factory.CreateAdminClientAsync();
        var category = await TestData.CreateCategoryAsync(client);
        var product = await TestData.CreateProductAsync(client, category.Id, quantity: 2);

        var response = await client.PostAsJsonAsync($"/api/inventory/{product.Id}/adjust", new AdjustInventoryRequest
        {
            Delta = 0
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AdjustQuantity_ReturnsNotFound_ForUnknownProduct()
    {
        var client = await _factory.CreateAdminClientAsync();

        var response = await client.PostAsJsonAsync("/api/inventory/999999/adjust", new AdjustInventoryRequest
        {
            Delta = 1
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
