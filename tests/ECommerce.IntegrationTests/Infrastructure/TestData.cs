using System.Net.Http.Json;
using ECommerce.Application.DTOs;
using ECommerce.Application.DTOs.Addresses;
using ECommerce.Application.DTOs.Auth;
using ECommerce.Application.DTOs.Cart;
using ECommerce.Application.DTOs.Categories;
using ECommerce.Application.DTOs.Orders;
using ECommerce.Application.DTOs.Payments;
using ECommerce.Application.DTOs.Products;

namespace ECommerce.IntegrationTests.Infrastructure;

public static class TestData
{
    public const string Password = "Passw0rd!";

    /// <summary>
    /// The mock provider approves this card and declines any card ending in 0000.
    /// </summary>
    public const string ApprovedCard = "4111111111111111";

    public const string DeclinedCard = "4111111111110000";

    public static string UniqueName(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..14];

    public static string UniqueEmail() => $"{Guid.NewGuid():N}@test.com";

    public static RegisterRequest NewUser(string? email = null) => new()
    {
        FirstName = "Test",
        LastName = "User",
        Email = email ?? UniqueEmail(),
        Password = Password
    };

    public static async Task<AuthResponse> RegisterAsync(HttpClient client, string? email = null)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", NewUser(email));

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    public static async Task<AuthResponse> LoginAsync(HttpClient client, string email, string password = Password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = password
        });

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    public static async Task<CategoryResponse> CreateCategoryAsync(HttpClient client, string? name = null)
    {
        var response = await client.PostAsJsonAsync("/api/categories", new CreateCategoryRequest
        {
            Name = name ?? UniqueName("Cat")
        });

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<CategoryResponse>())!;
    }

    public static async Task<ProductResponse> CreateProductAsync(
        HttpClient client,
        int categoryId,
        string? name = null,
        string? sku = null,
        decimal price = 100m,
        int quantity = 10)
    {
        var response = await client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            Name = name ?? UniqueName("Prod"),
            Price = price,
            Sku = sku ?? UniqueName("SKU"),
            CategoryId = categoryId,
            Quantity = quantity
        });

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ProductResponse>())!;
    }

    public static CreateAddressRequest NewAddress(string? city = null) => new()
    {
        FullName = "Test Recipient",
        AddressLine = "1 Test Street",
        City = city ?? UniqueName("City"),
        State = "TestState",
        PostalCode = "12345",
        Country = "TestCountry",
        PhoneNumber = "5550100"
    };

    public static async Task<AddressResponse> CreateAddressAsync(HttpClient client, string? city = null)
    {
        var response = await client.PostAsJsonAsync("/api/addresses", NewAddress(city));

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<AddressResponse>())!;
    }

    public static async Task<CartResponse> AddCartItemAsync(HttpClient client, int productId, int quantity = 1)
    {
        var response = await client.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest
        {
            ProductId = productId,
            Quantity = quantity
        });

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<CartResponse>())!;
    }

    public static async Task<CartResponse> GetCartAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/cart");

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<CartResponse>())!;
    }

    public static async Task<IReadOnlyList<OrderResponse>> GetOrdersAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/orders?page=1&pageSize=50");

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<PagedResult<OrderResponse>>())!.Items;
    }

    public static async Task<OrderResponse> CreateOrderAsync(HttpClient client, int shippingAddressId)
    {
        var response = await client.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            ShippingAddressId = shippingAddressId
        });

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<OrderResponse>())!;
    }

    public static CheckoutRequest NewCheckout(int shippingAddressId, string cardNumber) => new()
    {
        ShippingAddressId = shippingAddressId,
        CardNumber = cardNumber
    };

    public static async Task<CheckoutResponse> CheckoutAsync(
        HttpClient client,
        int shippingAddressId,
        string cardNumber = ApprovedCard)
    {
        var response = await client.PostAsJsonAsync("/api/checkout", NewCheckout(shippingAddressId, cardNumber));

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<CheckoutResponse>())!;
    }

    public static async Task<PaymentResponse> PayOrderAsync(
        HttpClient client,
        int orderId,
        string cardNumber = ApprovedCard)
    {
        var response = await client.PostAsJsonAsync("/api/payments", new PaymentRequest
        {
            OrderId = orderId,
            CardNumber = cardNumber
        });

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<PaymentResponse>())!;
    }
}
