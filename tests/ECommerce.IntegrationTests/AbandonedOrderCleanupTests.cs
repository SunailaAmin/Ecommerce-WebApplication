using System.Net.Http.Json;
using ECommerce.Application.DTOs.Orders;
using ECommerce.Domain.Enums;
using ECommerce.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;

namespace ECommerce.IntegrationTests;

/// <summary>
/// The cleanup job is off by default, so this host switches it on with a very short window
/// and a very short interval. The order it cancels is a real one placed through the API.
/// </summary>
public class AbandonedOrderCleanupTests : IClassFixture<AbandonedOrderCleanupTests.CleanupFactory>
{
    private readonly CleanupFactory _factory;

    public AbandonedOrderCleanupTests(CleanupFactory factory)
    {
        _factory = factory;
    }

    public class CleanupFactory : ApiFactory
    {
        protected override void ConfigureTestHost(IWebHostBuilder builder)
        {
            // Cancels any Pending order older than about a second, checking every 200ms.
            builder.UseSetting("OrderProcessing:AbandonedOrderTimeoutMinutes", "0.016");
            builder.UseSetting("OrderProcessing:CleanupIntervalMinutes", "0.003");
        }
    }

    private static async Task<OrderResponse?> WaitForStatusAsync(
        HttpClient client,
        int orderId,
        OrderStatus status,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            var response = await client.GetAsync($"/api/orders/{orderId}");
            response.EnsureSuccessStatusCode();

            var order = await response.Content.ReadFromJsonAsync<OrderResponse>();

            if (order?.Status == status)
            {
                return order;
            }

            await Task.Delay(200);
        }

        return null;
    }

    [Fact]
    public async Task AnUnpaidOrderIsCancelledByTheCleanupJob()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var customer = await _factory.CreateFreshCustomerAsync();

        var category = await TestData.CreateCategoryAsync(admin);
        var product = await TestData.CreateProductAsync(admin, category.Id, quantity: 5);

        var address = await TestData.CreateAddressAsync(customer.Client);
        await TestData.AddCartItemAsync(customer.Client, product.Id, 2);

        // Placed but never paid for, so it stays Pending and keeps holding the stock.
        var order = await TestData.CreateOrderAsync(customer.Client, address.Id);
        Assert.Equal(OrderStatus.Pending, order.Status);

        var cancelled = await WaitForStatusAsync(
            customer.Client,
            order.Id,
            OrderStatus.Cancelled,
            TimeSpan.FromSeconds(30));

        Assert.NotNull(cancelled);
        Assert.Equal(OrderStatus.Cancelled, cancelled!.Status);
    }

    [Fact]
    public async Task APaidOrderIsLeftAlone()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var customer = await _factory.CreateFreshCustomerAsync();

        var category = await TestData.CreateCategoryAsync(admin);
        var product = await TestData.CreateProductAsync(admin, category.Id, quantity: 5);

        var address = await TestData.CreateAddressAsync(customer.Client);
        await TestData.AddCartItemAsync(customer.Client, product.Id, 1);

        // Checkout confirms the order, so it is no longer awaiting payment.
        var checkout = await TestData.CheckoutAsync(customer.Client, address.Id);
        Assert.Equal(OrderStatus.Confirmed, checkout.Order.Status);

        await Task.Delay(3000);

        var response = await customer.Client.GetAsync($"/api/orders/{checkout.Order.Id}");
        response.EnsureSuccessStatusCode();

        var order = await response.Content.ReadFromJsonAsync<OrderResponse>();

        Assert.Equal(OrderStatus.Confirmed, order!.Status);
    }
}
