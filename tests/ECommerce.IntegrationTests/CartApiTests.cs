using System.Net;
using System.Net.Http.Json;
using ECommerce.Application.DTOs.Cart;
using ECommerce.Application.DTOs.Products;
using ECommerce.IntegrationTests.Infrastructure;
using Xunit;

namespace ECommerce.IntegrationTests;

public class CartApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public CartApiTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient Customer, HttpClient Admin, ProductResponse Product)> SetupAsync(int quantity = 10, decimal price = 50m)
    {
        var admin = await _factory.CreateAdminClientAsync();
        var customer = await _factory.CreateFreshCustomerClientAsync();
        var category = await TestData.CreateCategoryAsync(admin);
        var product = await TestData.CreateProductAsync(admin, category.Id, price: price, quantity: quantity);

        return (customer, admin, product);
    }

    [Fact]
    public async Task GetCart_RequiresAuthentication()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/cart");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCart_ReturnsEmptyCartForNewCustomer()
    {
        var customer = await _factory.CreateFreshCustomerClientAsync();

        var cart = await TestData.GetCartAsync(customer);

        Assert.Empty(cart.Items);
        Assert.Equal(0m, cart.Subtotal);
        Assert.Equal(0, cart.TotalQuantity);
    }

    [Fact]
    public async Task AddItem_AddsProductToCart()
    {
        var (customer, _, product) = await SetupAsync();

        var cart = await TestData.AddCartItemAsync(customer, product.Id, 2);

        Assert.Single(cart.Items);
        Assert.Equal(product.Id, cart.Items[0].ProductId);
        Assert.Equal(2, cart.Items[0].Quantity);
        Assert.Equal(2, cart.TotalQuantity);
    }

    [Fact]
    public async Task AddItem_IncrementsQuantity_WhenProductAlreadyInCart()
    {
        var (customer, _, product) = await SetupAsync();

        await TestData.AddCartItemAsync(customer, product.Id, 2);
        var cart = await TestData.AddCartItemAsync(customer, product.Id, 3);

        Assert.Single(cart.Items);
        Assert.Equal(5, cart.Items[0].Quantity);
    }

    [Fact]
    public async Task AddItem_ReturnsNotFound_ForUnknownProduct()
    {
        var customer = await _factory.CreateFreshCustomerClientAsync();

        var response = await customer.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest
        {
            ProductId = 999999,
            Quantity = 1
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddItem_ReturnsBadRequest_WhenQuantityExceedsStock()
    {
        var (customer, _, product) = await SetupAsync(quantity: 3);

        var response = await customer.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest
        {
            ProductId = product.Id,
            Quantity = 4
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddItem_ReturnsBadRequest_WhenCombinedQuantityExceedsStock()
    {
        var (customer, _, product) = await SetupAsync(quantity: 5);

        await TestData.AddCartItemAsync(customer, product.Id, 4);

        var response = await customer.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest
        {
            ProductId = product.Id,
            Quantity = 2
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddItem_ReturnsBadRequest_WhenQuantityIsZero()
    {
        var (customer, _, product) = await SetupAsync();

        var response = await customer.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest
        {
            ProductId = product.Id,
            Quantity = 0
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CartTotals_AreCalculatedOnTheServer()
    {
        var (customer, _, product) = await SetupAsync(price: 50m);

        var cart = await TestData.AddCartItemAsync(customer, product.Id, 2);

        Assert.Equal(100m, cart.Subtotal);
        Assert.Equal(50m, cart.Items[0].UnitPrice);
        Assert.Equal(100m, cart.Items[0].LineTotal);
    }

    [Fact]
    public async Task Cart_TracksTheCurrentProductPrice_NotAStaleCartPrice()
    {
        var (customer, admin, product) = await SetupAsync(price: 50m);

        var before = await TestData.AddCartItemAsync(customer, product.Id, 2);
        Assert.Equal(100m, before.Subtotal);

        // An admin changes the price after the product is already in the cart.
        var update = await admin.PutAsJsonAsync($"/api/products/{product.Id}", new UpdateProductRequest
        {
            Name = product.Name,
            Price = 80m,
            Sku = product.Sku,
            CategoryId = product.CategoryId
        });
        update.EnsureSuccessStatusCode();

        var after = await TestData.GetCartAsync(customer);

        Assert.Equal(160m, after.Subtotal);
        Assert.Equal(80m, after.Items[0].UnitPrice);
    }

    [Fact]
    public async Task UpdateItem_SetsExactQuantity()
    {
        var (customer, _, product) = await SetupAsync();

        var cart = await TestData.AddCartItemAsync(customer, product.Id, 2);

        var response = await customer.PutAsJsonAsync($"/api/cart/items/{cart.Items[0].Id}", new UpdateCartItemRequest
        {
            Quantity = 5
        });
        var updated = await response.Content.ReadFromJsonAsync<CartResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(5, updated!.Items[0].Quantity);
    }

    [Fact]
    public async Task UpdateItem_ReturnsBadRequest_WhenQuantityExceedsStock()
    {
        var (customer, _, product) = await SetupAsync(quantity: 3);

        var cart = await TestData.AddCartItemAsync(customer, product.Id, 1);

        var response = await customer.PutAsJsonAsync($"/api/cart/items/{cart.Items[0].Id}", new UpdateCartItemRequest
        {
            Quantity = 9
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateItem_ReturnsNotFound_ForItemNotInCart()
    {
        var customer = await _factory.CreateFreshCustomerClientAsync();

        var response = await customer.PutAsJsonAsync("/api/cart/items/999999", new UpdateCartItemRequest
        {
            Quantity = 1
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateItem_ReturnsNotFound_ForAnotherCustomersCartItem()
    {
        var (customer, _, product) = await SetupAsync();
        var other = await _factory.CreateFreshCustomerClientAsync();

        var cart = await TestData.AddCartItemAsync(customer, product.Id, 1);

        var response = await other.PutAsJsonAsync($"/api/cart/items/{cart.Items[0].Id}", new UpdateCartItemRequest
        {
            Quantity = 5
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RemoveItem_RemovesTheLine()
    {
        var (customer, _, product) = await SetupAsync();

        var cart = await TestData.AddCartItemAsync(customer, product.Id, 2);

        var response = await customer.DeleteAsync($"/api/cart/items/{cart.Items[0].Id}");
        var updated = await response.Content.ReadFromJsonAsync<CartResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(updated!.Items);
        Assert.Equal(0m, updated.Subtotal);
    }

    [Fact]
    public async Task RemoveItem_ReturnsNotFound_ForItemNotInCart()
    {
        var customer = await _factory.CreateFreshCustomerClientAsync();

        var response = await customer.DeleteAsync("/api/cart/items/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ClearCart_EmptiesTheCart()
    {
        var (customer, _, product) = await SetupAsync();

        await TestData.AddCartItemAsync(customer, product.Id, 2);

        var response = await customer.DeleteAsync("/api/cart");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var cart = await TestData.GetCartAsync(customer);
        Assert.Empty(cart.Items);
    }

    [Fact]
    public async Task ClearCart_Succeeds_WhenCartIsAlreadyEmpty()
    {
        var customer = await _factory.CreateFreshCustomerClientAsync();

        var response = await customer.DeleteAsync("/api/cart");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Carts_AreIsolatedBetweenCustomers()
    {
        var (customer, _, product) = await SetupAsync();
        var other = await _factory.CreateFreshCustomerClientAsync();

        await TestData.AddCartItemAsync(customer, product.Id, 1);

        var otherCart = await TestData.GetCartAsync(other);

        Assert.Empty(otherCart.Items);
    }

    [Fact]
    public async Task Cart_RequiresAuthentication_ForEveryWrite()
    {
        var client = _factory.CreateClient();

        var post = await client.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest { ProductId = 1, Quantity = 1 });
        var put = await client.PutAsJsonAsync("/api/cart/items/1", new UpdateCartItemRequest { Quantity = 1 });
        var deleteItem = await client.DeleteAsync("/api/cart/items/1");
        var clear = await client.DeleteAsync("/api/cart");

        Assert.Equal(HttpStatusCode.Unauthorized, post.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, put.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, deleteItem.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, clear.StatusCode);
    }
}
