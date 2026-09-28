using System.Net;
using System.Net.Http.Json;
using ECommerce.Application.DTOs.Payments;
using ECommerce.Application.DTOs.Products;
using ECommerce.Domain.Enums;
using ECommerce.Infrastructure.Data;
using ECommerce.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.IntegrationTests;

public class CheckoutApiTests : IClassFixture<ApiFactory>
{
    private const string CardDigits = "4111111111111111";

    private readonly ApiFactory _factory;

    public CheckoutApiTests(ApiFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// A customer with one product in the cart and an address of their own.
    /// </summary>
    private async Task<(HttpClient Customer, HttpClient Admin, int ProductId, int AddressId)> ArrangeAsync(
        decimal price = 100m,
        int stock = 10,
        int cartQuantity = 2)
    {
        var admin = await _factory.CreateAdminClientAsync();
        var customer = await _factory.CreateFreshCustomerClientAsync();

        var category = await TestData.CreateCategoryAsync(admin);
        var product = await TestData.CreateProductAsync(admin, category.Id, price: price, quantity: stock);
        var address = await TestData.CreateAddressAsync(customer);

        await TestData.AddCartItemAsync(customer, product.Id, cartQuantity);

        return (customer, admin, product.Id, address.Id);
    }

    private async Task<int> GetStockAsync(int productId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ECommerceDbContext>();

        return await context.Inventories
            .Where(x => x.ProductId == productId)
            .Select(x => x.Quantity)
            .SingleAsync();
    }

    [Fact]
    public async Task Checkout_CreatesAConfirmedOrder_WhenThePaymentSucceeds()
    {
        var (customer, _, _, addressId) = await ArrangeAsync();

        var result = await TestData.CheckoutAsync(customer, addressId);

        Assert.Equal(OrderStatus.Confirmed, result.Order.Status);
        Assert.Equal(PaymentStatus.Successful, result.Payment.Status);
        Assert.Equal(result.Order.Id, result.Payment.OrderId);
        Assert.Equal(result.Order.TotalAmount, result.Payment.Amount);
        Assert.False(string.IsNullOrWhiteSpace(result.Payment.TransactionId));
        Assert.Null(result.Payment.FailureReason);
        Assert.StartsWith("ORD-", result.Order.OrderNumber);
    }

    [Fact]
    public async Task Checkout_ReservesStockAndEmptiesTheCart()
    {
        var (customer, _, productId, addressId) = await ArrangeAsync(stock: 10, cartQuantity: 2);

        await TestData.CheckoutAsync(customer, addressId);

        var cart = await TestData.GetCartAsync(customer);
        Assert.Empty(cart.Items);
        Assert.Equal(8, await GetStockAsync(productId));
    }

    [Fact]
    public async Task Checkout_LeavesTheOrderPending_WhenTheCardIsDeclined()
    {
        var (customer, _, _, addressId) = await ArrangeAsync();

        var result = await TestData.CheckoutAsync(customer, addressId, TestData.DeclinedCard);

        // The order survives so the customer can retry with another card.
        Assert.Equal(OrderStatus.Pending, result.Order.Status);
        Assert.Equal(PaymentStatus.Failed, result.Payment.Status);
        Assert.Equal("The card was declined by the payment provider.", result.Payment.FailureReason);
        Assert.Null(result.Payment.TransactionId);
    }

    [Fact]
    public async Task Checkout_KeepsTheReservedStock_WhenTheCardIsDeclined()
    {
        var (customer, _, productId, addressId) = await ArrangeAsync(stock: 10, cartQuantity: 2);

        await TestData.CheckoutAsync(customer, addressId, TestData.DeclinedCard);

        Assert.Equal(8, await GetStockAsync(productId));
    }

    [Fact]
    public async Task Checkout_CalculatesTheTotalFromCurrentProductPrices()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var customer = await _factory.CreateFreshCustomerClientAsync();

        var category = await TestData.CreateCategoryAsync(admin);
        var product = await TestData.CreateProductAsync(admin, category.Id, price: 50m, quantity: 10);
        var address = await TestData.CreateAddressAsync(customer);

        await TestData.AddCartItemAsync(customer, product.Id, 2);

        var update = await admin.PutAsJsonAsync($"/api/products/{product.Id}", new UpdateProductRequest
        {
            Name = product.Name,
            Price = 80m,
            Sku = product.Sku,
            CategoryId = category.Id
        });
        update.EnsureSuccessStatusCode();

        var result = await TestData.CheckoutAsync(customer, address.Id);

        // 80 x 2, read from the product row rather than from the cart.
        Assert.Equal(160m, result.Order.TotalAmount);
        Assert.Equal(160m, result.Payment.Amount);
        Assert.Equal(80m, result.Order.Items[0].UnitPrice);
    }

    [Fact]
    public async Task Checkout_ReturnsBadRequest_WhenTheCartIsEmpty()
    {
        var customer = await _factory.CreateFreshCustomerClientAsync();
        var address = await TestData.CreateAddressAsync(customer);

        var response = await customer.PostAsJsonAsync(
            "/api/checkout",
            TestData.NewCheckout(address.Id, TestData.ApprovedCard));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Checkout_DoesNotCreateASecondOrder_WhenSubmittedTwice()
    {
        var (customer, _, _, addressId) = await ArrangeAsync();

        var first = await TestData.CheckoutAsync(customer, addressId);

        var second = await customer.PostAsJsonAsync(
            "/api/checkout",
            TestData.NewCheckout(addressId, TestData.ApprovedCard));

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);

        var orders = await TestData.GetOrdersAsync(customer);
        Assert.Single(orders);
        Assert.Equal(first.Order.Id, orders[0].Id);
    }

    [Fact]
    public async Task Checkout_ReturnsNotFound_ForAnotherCustomersAddress()
    {
        var (_, admin, _, otherAddressId) = await ArrangeAsync();

        var other = await _factory.CreateFreshCustomerClientAsync();
        var category = await TestData.CreateCategoryAsync(admin);
        var product = await TestData.CreateProductAsync(admin, category.Id, price: 10m, quantity: 5);

        await TestData.AddCartItemAsync(other, product.Id, 1);

        var response = await other.PostAsJsonAsync(
            "/api/checkout",
            TestData.NewCheckout(otherAddressId, TestData.ApprovedCard));

        // Not found rather than forbidden, so the address is not confirmed to exist.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Checkout_ReturnsUnauthorized_WhenSignedOut()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync(
            "/api/checkout",
            TestData.NewCheckout(1, TestData.ApprovedCard));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("41111111111111111111111111111")]
    public async Task Checkout_ReturnsBadRequest_ForAnInvalidCardNumber(string cardNumber)
    {
        var customer = await _factory.CreateFreshCustomerClientAsync();
        var address = await TestData.CreateAddressAsync(customer);

        var response = await customer.PostAsJsonAsync("/api/checkout", TestData.NewCheckout(address.Id, cardNumber));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Checkout_ReturnsBadRequest_ForAnInvalidShippingAddressId()
    {
        var customer = await _factory.CreateFreshCustomerClientAsync();

        var response = await customer.PostAsJsonAsync(
            "/api/checkout",
            new CheckoutRequest { ShippingAddressId = 0, CardNumber = TestData.ApprovedCard });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Checkout_ReturnsBadRequest_WhenTheStockWasTakenByAnotherOrder()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var category = await TestData.CreateCategoryAsync(admin);

        var product = await TestData.CreateProductAsync(admin, category.Id, price: 10m, quantity: 1);

        var first = await _factory.CreateFreshCustomerClientAsync();
        var firstAddress = await TestData.CreateAddressAsync(first);
        await TestData.AddCartItemAsync(first, product.Id, 1);

        var second = await _factory.CreateFreshCustomerClientAsync();
        var secondAddress = await TestData.CreateAddressAsync(second);
        await TestData.AddCartItemAsync(second, product.Id, 1);

        await TestData.CheckoutAsync(first, firstAddress.Id);

        // Stock is re-read at checkout rather than trusted from the cart.
        var response = await second.PostAsJsonAsync(
            "/api/checkout",
            TestData.NewCheckout(secondAddress.Id, TestData.ApprovedCard));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Checkout_NeverStoresTheCardNumber()
    {
        var (customer, _, _, addressId) = await ArrangeAsync();

        await TestData.CheckoutAsync(customer, addressId);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ECommerceDbContext>();

        var payments = await context.Payments.AsNoTracking().ToListAsync();

        Assert.NotEmpty(payments);
        Assert.DoesNotContain(
            typeof(Domain.Entities.Payment).GetProperties(),
            p => p.Name.Contains("Card", StringComparison.OrdinalIgnoreCase));

        foreach (var payment in payments)
        {
            Assert.DoesNotContain(CardDigits, payment.TransactionId ?? string.Empty);
            Assert.DoesNotContain(CardDigits, payment.FailureReason ?? string.Empty);
        }
    }
}
