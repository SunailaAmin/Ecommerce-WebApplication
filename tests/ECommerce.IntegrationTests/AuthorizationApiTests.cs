using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ECommerce.Application.DTOs.Categories;
using ECommerce.Application.DTOs.Inventory;
using ECommerce.Application.DTOs.Products;
using ECommerce.IntegrationTests.Infrastructure;
using Xunit;

namespace ECommerce.IntegrationTests;

public class AuthorizationApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AuthorizationApiTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateProduct_ReturnsUnauthorized_ForAnonymous()
    {
        var client = _factory.CreateClient();
        var category = await SeedCategoryAsync();

        var response = await client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            Name = TestData.UniqueName("Anon"),
            Price = 10m,
            Sku = TestData.UniqueName("ANON"),
            CategoryId = category.Id,
            Quantity = 1
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateProduct_ReturnsForbidden_ForCustomer()
    {
        var client = await _factory.CreateCustomerClientAsync();
        var category = await SeedCategoryAsync();

        var response = await client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            Name = TestData.UniqueName("Cust"),
            Price = 10m,
            Sku = TestData.UniqueName("CUST"),
            CategoryId = category.Id,
            Quantity = 1
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateProduct_ReturnsCreated_ForAdmin()
    {
        var client = await _factory.CreateAdminClientAsync();
        var category = await SeedCategoryAsync();

        var response = await client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            Name = TestData.UniqueName("Admin"),
            Price = 10m,
            Sku = TestData.UniqueName("ADMN"),
            CategoryId = category.Id,
            Quantity = 1
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreateCategory_ReturnsForbidden_ForCustomer()
    {
        var client = await _factory.CreateCustomerClientAsync();

        var response = await client.PostAsJsonAsync(
            "/api/categories",
            new CreateCategoryRequest { Name = TestData.UniqueName("Cat") });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProduct_ReturnsForbidden_ForCustomer()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var customer = await _factory.CreateCustomerClientAsync();

        var category = await TestData.CreateCategoryAsync(admin);
        var product = await TestData.CreateProductAsync(admin, category.Id);

        var response = await customer.PutAsJsonAsync($"/api/products/{product.Id}", new UpdateProductRequest
        {
            Name = "Hacked",
            Price = 1m,
            Sku = product.Sku,
            CategoryId = category.Id
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteProduct_ReturnsUnauthorized_ForAnonymous()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var anonymous = _factory.CreateClient();

        var category = await TestData.CreateCategoryAsync(admin);
        var product = await TestData.CreateProductAsync(admin, category.Id);

        var response = await anonymous.DeleteAsync($"/api/products/{product.Id}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AdjustInventory_ReturnsForbidden_ForCustomer()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var customer = await _factory.CreateCustomerClientAsync();

        var category = await TestData.CreateCategoryAsync(admin);
        var product = await TestData.CreateProductAsync(admin, category.Id, quantity: 5);

        var response = await customer.PostAsJsonAsync(
            $"/api/inventory/{product.Id}/adjust",
            new AdjustInventoryRequest { Delta = 1 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SetInventory_ReturnsForbidden_ForCustomer()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var customer = await _factory.CreateCustomerClientAsync();

        var category = await TestData.CreateCategoryAsync(admin);
        var product = await TestData.CreateProductAsync(admin, category.Id);

        var response = await customer.PutAsJsonAsync(
            $"/api/inventory/{product.Id}",
            new UpdateInventoryRequest { Quantity = 999 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListInventory_ReturnsForbidden_ForCustomer()
    {
        var customer = await _factory.CreateCustomerClientAsync();

        var response = await customer.GetAsync("/api/inventory");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListInventory_ReturnsUnauthorized_ForAnonymous()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/inventory");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ReadEndpoints_ArePublic()
    {
        var client = _factory.CreateClient();
        var admin = await _factory.CreateAdminClientAsync();

        var category = await TestData.CreateCategoryAsync(admin);
        var product = await TestData.CreateProductAsync(admin, category.Id);

        var products = await client.GetAsync("/api/products");
        var productById = await client.GetAsync($"/api/products/{product.Id}");
        var categories = await client.GetAsync("/api/categories");
        var inventory = await client.GetAsync($"/api/inventory/{product.Id}");

        Assert.Equal(HttpStatusCode.OK, products.StatusCode);
        Assert.Equal(HttpStatusCode.OK, productById.StatusCode);
        Assert.Equal(HttpStatusCode.OK, categories.StatusCode);
        Assert.Equal(HttpStatusCode.OK, inventory.StatusCode);
    }

    [Fact]
    public async Task RegistrationNeverGrantsAdmin_AndRegistrationTokenCannotWrite()
    {
        var client = _factory.CreateClient();

        var auth = await TestData.RegisterAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);

        var category = await SeedCategoryAsync();
        var response = await client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            Name = TestData.UniqueName("Self"),
            Price = 10m,
            Sku = TestData.UniqueName("SELF"),
            CategoryId = category.Id,
            Quantity = 1
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<CategoryResponse> SeedCategoryAsync()
    {
        var admin = await _factory.CreateAdminClientAsync();
        return await TestData.CreateCategoryAsync(admin);
    }
}
