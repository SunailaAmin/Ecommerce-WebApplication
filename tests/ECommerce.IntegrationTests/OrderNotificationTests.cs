using System.Collections.Concurrent;
using System.Net.Http.Json;
using ECommerce.API.Hubs;
using ECommerce.Application.DTOs.Orders;
using ECommerce.Application.Notifications;
using ECommerce.Domain.Enums;
using ECommerce.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace ECommerce.IntegrationTests;

/// <summary>
/// Drives the real hub over a real connection: the client authenticates with the same JWT
/// the REST API uses, and an admin status change has to arrive at the order's owner.
/// </summary>
public class OrderNotificationTests : IClassFixture<ApiFactory>
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    private readonly ApiFactory _factory;

    public OrderNotificationTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private sealed class HubListener : IAsyncDisposable
    {
        public HubListener(HubConnection connection)
        {
            Connection = connection;

            Connection.On<OrderNotification>(
                OrderNotificationHub.ClientMethod,
                notification => Received.Add(notification));
        }

        public HubConnection Connection { get; }

        public ConcurrentBag<OrderNotification> Received { get; } = [];

        public async ValueTask DisposeAsync()
        {
            await Connection.StopAsync();
            await Connection.DisposeAsync();
        }
    }

    /// <summary>
    /// Connects to the hub as the given user and starts recording what arrives.
    /// </summary>
    private async Task<HubListener> ConnectAsync(AuthenticatedUser user)
    {
        var url = $"{_factory.Server.BaseAddress.ToString().TrimEnd('/')}{OrderNotificationHub.Route}";

        var handler = _factory.Server.CreateHandler();

        var connection = new HubConnectionBuilder()
            .WithUrl(url, options =>
            {
                // Routes the SignalR traffic through the in-memory test server instead of
                // out to a real socket, which nothing is listening on during tests.
                options.HttpMessageHandlerFactory = _ => handler;

                // The in-memory server cannot complete a WebSocket upgrade.
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult<string?>(user.Token);
            })
            .Build();

        var listener = new HubListener(connection);

        await connection.StartAsync();

        return listener;
    }

    private static async Task<List<OrderNotification>> WaitForAsync(
        HubListener listener,
        Func<OrderNotification, bool> match)
    {
        var deadline = DateTime.UtcNow + Timeout;

        while (DateTime.UtcNow < deadline)
        {
            var hit = listener.Received.FirstOrDefault(match);

            if (hit is not null)
            {
                return [hit];
            }

            await Task.Delay(50);
        }

        return [];
    }

    /// <summary>
    /// Registers a customer, buys a product, and leaves the order Confirmed and ready for
    /// an admin to move along the fulfilment flow.
    /// </summary>
    private async Task<(AuthenticatedUser Customer, OrderResponse Order)> PlaceOrderAsync()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var customer = await _factory.CreateFreshCustomerAsync();

        var category = await TestData.CreateCategoryAsync(admin);
        var product = await TestData.CreateProductAsync(admin, category.Id, quantity: 5);

        var address = await TestData.CreateAddressAsync(customer.Client);
        await TestData.AddCartItemAsync(customer.Client, product.Id, 2);

        var checkout = await TestData.CheckoutAsync(customer.Client, address.Id);

        return (customer, checkout.Order);
    }

    private async Task<HttpResponseMessage> AdvanceStatusAsync(HttpClient admin, OrderResponse order, OrderStatus status)
    {
        var response = await admin.PutAsJsonAsync(
            $"/api/admin/orders/{order.Id}/status",
            new UpdateOrderStatusRequest { Status = status });

        response.EnsureSuccessStatusCode();

        return response;
    }

    [Fact]
    public async Task AnAdminStatusChangeIsPushedToTheOrdersOwner()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var (customer, order) = await PlaceOrderAsync();

        await using var listener = await ConnectAsync(customer);

        await AdvanceStatusAsync(admin, order, OrderStatus.Processing);

        var received = await WaitForAsync(
            listener,
            n => n.OrderId == order.Id && n.Status == OrderStatus.Processing);

        Assert.Single(received);
        Assert.Equal(customer.UserId, received[0].UserId);
        Assert.Equal(order.OrderNumber, received[0].OrderNumber);
        Assert.Contains(order.OrderNumber, received[0].Message);
    }

    [Fact]
    public async Task ANotificationIsNotDeliveredToAnotherCustomer()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var (customer, order) = await PlaceOrderAsync();
        var bystander = await _factory.CreateFreshCustomerAsync();

        await using var ownerListener = await ConnectAsync(customer);
        await using var bystanderListener = await ConnectAsync(bystander);

        await AdvanceStatusAsync(admin, order, OrderStatus.Processing);

        Assert.Single(await WaitForAsync(ownerListener, n => n.OrderId == order.Id));

        // The bystander is connected to the same hub, so this is the real check that routing
        // is per user rather than a broadcast.
        await Task.Delay(1000);

        Assert.Empty(bystanderListener.Received);
    }

    [Fact]
    public async Task ACustomerIsNotifiedWhenCheckoutConfirmsTheirOrder()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var customer = await _factory.CreateFreshCustomerAsync();

        var category = await TestData.CreateCategoryAsync(admin);
        var product = await TestData.CreateProductAsync(admin, category.Id, quantity: 3);
        var address = await TestData.CreateAddressAsync(customer.Client);
        await TestData.AddCartItemAsync(customer.Client, product.Id, 1);

        // Connected before checkout so the confirmation is not missed.
        await using var listener = await ConnectAsync(customer);

        var checkout = await TestData.CheckoutAsync(customer.Client, address.Id);

        var received = await WaitForAsync(
            listener,
            n => n.OrderId == checkout.Order.Id && n.Status == OrderStatus.Confirmed);

        Assert.Single(received);
        Assert.Equal(customer.UserId, received[0].UserId);
    }

    [Fact]
    public async Task TheHubRefusesAConnection_WithoutAToken()
    {
        var url = $"{_factory.Server.BaseAddress.ToString().TrimEnd('/')}{OrderNotificationHub.Route}";

        await using var connection = new HubConnectionBuilder().WithUrl(url).Build();

        // Notifications are user-specific, so an anonymous connection is not allowed.
        await Assert.ThrowsAnyAsync<Exception>(() => connection.StartAsync());
    }

    [Fact]
    public async Task ADeclinedPaymentDoesNotAnnounceAConfirmation()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var customer = await _factory.CreateFreshCustomerAsync();

        var category = await TestData.CreateCategoryAsync(admin);
        var product = await TestData.CreateProductAsync(admin, category.Id, quantity: 3);
        var address = await TestData.CreateAddressAsync(customer.Client);
        await TestData.AddCartItemAsync(customer.Client, product.Id, 1);

        await using var listener = await ConnectAsync(customer);

        var checkout = await TestData.CheckoutAsync(customer.Client, address.Id, TestData.DeclinedCard);

        // The order stays Pending, so there is nothing to announce.
        await Task.Delay(1500);

        Assert.DoesNotContain(listener.Received, n => n.OrderId == checkout.Order.Id);
    }
}
