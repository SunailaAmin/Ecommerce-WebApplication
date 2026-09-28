using System.Net;
using System.Net.Http.Json;
using ECommerce.Application.DTOs;
using ECommerce.Application.DTOs.Addresses;
using ECommerce.Application.DTOs.Inventory;
using ECommerce.Application.DTOs.Orders;
using ECommerce.Application.DTOs.Products;
using ECommerce.Domain.Enums;
using ECommerce.IntegrationTests.Infrastructure;
using Xunit;

namespace ECommerce.IntegrationTests;

public class OrdersApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public OrdersApiTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<ProductResponse> CreateProductAsync(HttpClient admin, int quantity = 10, decimal price = 50m)
    {
        var category = await TestData.CreateCategoryAsync(admin);

        return await TestData.CreateProductAsync(admin, category.Id, price: price, quantity: quantity);
    }

    /// <summary>
    /// A fresh customer with one product in the cart and a saved shipping address.
    /// </summary>
    private async Task<(HttpClient Customer, AddressResponse Address, int ProductId)> SetupReadyToOrderAsync(
        HttpClient? admin = null,
        int quantity = 1,
        decimal price = 50m,
        int stock = 10)
    {
        admin ??= await _factory.CreateAdminClientAsync();

        var customer = await _factory.CreateFreshCustomerClientAsync();
        var product = await CreateProductAsync(admin, stock, price);
        var address = await TestData.CreateAddressAsync(customer);

        await TestData.AddCartItemAsync(customer, product.Id, quantity);

        return (customer, address, product.Id);
    }

    [Fact]
    public async Task Orders_RequireAuthentication()
    {
        var client = _factory.CreateClient();

        var post = await client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { ShippingAddressId = 1 });
        var getAll = await client.GetAsync("/api/orders");
        var getById = await client.GetAsync("/api/orders/1");
        var adminList = await client.GetAsync("/api/admin/orders");
        var adminStatus = await client.PutAsJsonAsync("/api/admin/orders/1/status", new UpdateOrderStatusRequest
        {
            Status = OrderStatus.Confirmed
        });

        Assert.Equal(HttpStatusCode.Unauthorized, post.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, getAll.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, getById.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, adminList.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, adminStatus.StatusCode);
    }

    [Fact]
    public async Task CreateOrder_ReturnsBadRequest_WhenCartIsEmpty()
    {
        var customer = await _factory.CreateFreshCustomerClientAsync();
        var address = await TestData.CreateAddressAsync(customer);

        var response = await customer.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            ShippingAddressId = address.Id
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateOrder_CalculatesTheTotalOnTheServer()
    {
        var (customer, address, _) = await SetupReadyToOrderAsync(quantity: 2, price: 50m);

        var response = await customer.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            ShippingAddressId = address.Id
        });
        var order = await response.Content.ReadFromJsonAsync<OrderResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(100m, order!.TotalAmount);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.StartsWith("ORD-", order.OrderNumber);
        Assert.Single(order.Items);
        Assert.Equal(50m, order.Items[0].UnitPrice);
        Assert.Equal(100m, order.Items[0].TotalPrice);
    }

    [Fact]
    public async Task CreateOrder_EmptiesTheCart()
    {
        var (customer, address, _) = await SetupReadyToOrderAsync();

        await customer.PostAsJsonAsync("/api/orders", new CreateOrderRequest { ShippingAddressId = address.Id });

        var cart = await TestData.GetCartAsync(customer);

        Assert.Empty(cart.Items);
        Assert.Equal(0m, cart.Subtotal);
    }

    [Fact]
    public async Task CreateOrder_CommitsStock()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var (customer, address, productId) = await SetupReadyToOrderAsync(admin, quantity: 3, stock: 10);

        await customer.PostAsJsonAsync("/api/orders", new CreateOrderRequest { ShippingAddressId = address.Id });

        var inventory = await admin.GetFromJsonAsync<InventoryResponse>($"/api/inventory/{productId}");

        Assert.Equal(7, inventory!.Quantity);
    }

    [Fact]
    public async Task CreateOrder_ReturnsBadRequest_WhenStockChangedSinceTheItemWasAdded()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var (customer, address, productId) = await SetupReadyToOrderAsync(admin, quantity: 5, stock: 10);

        // Another order or admin takes the units before this one is placed.
        var response = await admin.PutAsJsonAsync($"/api/inventory/{productId}", new UpdateInventoryRequest
        {
            Quantity = 1
        });
        response.EnsureSuccessStatusCode();

        var order = await customer.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            ShippingAddressId = address.Id
        });

        Assert.Equal(HttpStatusCode.BadRequest, order.StatusCode);
    }

    [Fact]
    public async Task CreateOrder_ReturnsNotFound_WhenAddressBelongsToAnotherCustomer()
    {
        var (customer, _, _) = await SetupReadyToOrderAsync();
        var other = await _factory.CreateFreshCustomerClientAsync();
        var otherAddress = await TestData.CreateAddressAsync(other);

        var response = await customer.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            ShippingAddressId = otherAddress.Id
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateOrder_KeepsTheOrderReadable_AfterTheCartIsEmptied()
    {
        var (customer, address, _) = await SetupReadyToOrderAsync();

        var created = await TestData.CreateOrderAsync(customer, address.Id);

        var cart = await TestData.GetCartAsync(customer);
        var fetched = await customer.GetFromJsonAsync<OrderResponse>($"/api/orders/{created.Id}");

        Assert.Empty(cart.Items);
        Assert.NotNull(fetched);
        Assert.Equal(created.OrderNumber, fetched!.OrderNumber);
    }

    [Fact]
    public async Task GetMyOrders_ReturnsPagedEnvelope()
    {
        var (customer, address, _) = await SetupReadyToOrderAsync();
        await TestData.CreateOrderAsync(customer, address.Id);

        var response = await customer.GetAsync("/api/orders");
        var page = await response.Content.ReadFromJsonAsync<PagedResult<OrderResponse>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(page);
        Assert.Equal(1, page!.TotalCount);
        Assert.Single(page.Items);
        Assert.True(page.Page >= 1);
    }

    [Fact]
    public async Task GetMyOrders_ReturnsEmptyEnvelope_ForNewCustomer()
    {
        var customer = await _factory.CreateFreshCustomerClientAsync();

        var page = await customer.GetFromJsonAsync<PagedResult<OrderResponse>>("/api/orders");

        Assert.Equal(0, page!.TotalCount);
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task GetMyOrders_OnlyReturnsTheCustomersOwnOrders()
    {
        var (customer, address, _) = await SetupReadyToOrderAsync();
        var mine = await TestData.CreateOrderAsync(customer, address.Id);

        var other = await _factory.CreateFreshCustomerClientAsync();
        var otherAddress = await TestData.CreateAddressAsync(other);
        var admin = await _factory.CreateAdminClientAsync();
        var otherProduct = await CreateProductAsync(admin);
        await TestData.AddCartItemAsync(other, otherProduct.Id, 1);
        var theirs = await TestData.CreateOrderAsync(other, otherAddress.Id);

        var page = await customer.GetFromJsonAsync<PagedResult<OrderResponse>>("/api/orders");

        Assert.Equal(1, page!.TotalCount);
        Assert.Equal(mine.Id, page.Items[0].Id);
        Assert.DoesNotContain(page.Items, x => x.Id == theirs.Id);
    }

    [Fact]
    public async Task GetOrderById_ReturnsTheCustomersOrder()
    {
        var (customer, address, _) = await SetupReadyToOrderAsync();
        var created = await TestData.CreateOrderAsync(customer, address.Id);

        var response = await customer.GetAsync($"/api/orders/{created.Id}");
        var order = await response.Content.ReadFromJsonAsync<OrderResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(created.Id, order!.Id);
        Assert.Equal(address.Id, order.ShippingAddress.Id);
        Assert.Equal("Test Recipient", order.ShippingAddress.FullName);
        Assert.Single(order.Items);
    }

    [Fact]
    public async Task GetOrderById_ReturnsNotFound_ForAnotherCustomersOrder()
    {
        var (customer, address, _) = await SetupReadyToOrderAsync();
        var created = await TestData.CreateOrderAsync(customer, address.Id);

        var other = await _factory.CreateFreshCustomerClientAsync();

        var response = await other.GetAsync($"/api/orders/{created.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetOrderById_ReturnsNotFound_ForMissingOrder()
    {
        var customer = await _factory.CreateFreshCustomerClientAsync();

        var response = await customer.GetAsync("/api/orders/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AdminOrderRoutes_AreForbiddenForCustomers()
    {
        var customer = await _factory.CreateFreshCustomerClientAsync();

        var list = await customer.GetAsync("/api/admin/orders");
        var byId = await customer.GetAsync("/api/admin/orders/1");
        var status = await customer.PutAsJsonAsync("/api/admin/orders/1/status", new UpdateOrderStatusRequest
        {
            Status = OrderStatus.Confirmed
        });

        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byId.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, status.StatusCode);
    }

    [Fact]
    public async Task AdminList_ShowsEveryCustomersOrder()
    {
        var (customer, address, _) = await SetupReadyToOrderAsync();
        var created = await TestData.CreateOrderAsync(customer, address.Id);
        var admin = await _factory.CreateAdminClientAsync();

        var page = await admin.GetFromJsonAsync<PagedResult<OrderResponse>>("/api/admin/orders?pageSize=100");

        Assert.Contains(page!.Items, x => x.Id == created.Id);
    }

    [Fact]
    public async Task AdminList_SupportsStatusFilter()
    {
        var (customer, address, _) = await SetupReadyToOrderAsync();
        var created = await TestData.CreateOrderAsync(customer, address.Id);
        var admin = await _factory.CreateAdminClientAsync();

        var pending = await admin.GetFromJsonAsync<PagedResult<OrderResponse>>("/api/admin/orders?status=1&pageSize=100");
        var shipped = await admin.GetFromJsonAsync<PagedResult<OrderResponse>>("/api/admin/orders?status=4&pageSize=100");

        Assert.Contains(pending!.Items, x => x.Id == created.Id);
        Assert.DoesNotContain(shipped!.Items, x => x.Id == created.Id);
    }

    [Fact]
    public async Task AdminList_SupportsSearchByOrderNumber()
    {
        var (customer, address, _) = await SetupReadyToOrderAsync();
        var created = await TestData.CreateOrderAsync(customer, address.Id);
        var admin = await _factory.CreateAdminClientAsync();

        var page = await admin.GetFromJsonAsync<PagedResult<OrderResponse>>(
            $"/api/admin/orders?search={created.OrderNumber}");

        Assert.Equal(1, page!.TotalCount);
        Assert.Equal(created.Id, page.Items[0].Id);
    }

    [Fact]
    public async Task AdminCanReadAnyOrder()
    {
        var (customer, address, _) = await SetupReadyToOrderAsync();
        var created = await TestData.CreateOrderAsync(customer, address.Id);
        var admin = await _factory.CreateAdminClientAsync();

        var response = await admin.GetAsync($"/api/admin/orders/{created.Id}");
        var order = await response.Content.ReadFromJsonAsync<OrderResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(created.Id, order!.Id);
    }

    [Theory]
    [InlineData(OrderStatus.Confirmed, 2)]
    [InlineData(OrderStatus.Cancelled, 6)]
    public async Task AdminUpdateStatus_AdvancesTheOrder(OrderStatus target, int expectedValue)
    {
        var (customer, address, _) = await SetupReadyToOrderAsync();
        var created = await TestData.CreateOrderAsync(customer, address.Id);
        var admin = await _factory.CreateAdminClientAsync();

        var response = await admin.PutAsJsonAsync($"/api/admin/orders/{created.Id}/status", new UpdateOrderStatusRequest
        {
            Status = target
        });
        var order = await response.Content.ReadFromJsonAsync<OrderResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expectedValue, (int)order!.Status);
    }

    [Fact]
    public async Task AdminUpdateStatus_WalksTheOrderThroughTheFullLifecycle()
    {
        var (customer, address, _) = await SetupReadyToOrderAsync();
        var created = await TestData.CreateOrderAsync(customer, address.Id);
        var admin = await _factory.CreateAdminClientAsync();

        foreach (var status in new[]
                 {
                     OrderStatus.Confirmed,
                     OrderStatus.Processing,
                     OrderStatus.Shipped,
                     OrderStatus.Delivered
                 })
        {
            var response = await admin.PutAsJsonAsync(
                $"/api/admin/orders/{created.Id}/status",
                new UpdateOrderStatusRequest { Status = status });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var order = await customer.GetFromJsonAsync<OrderResponse>($"/api/orders/{created.Id}");

        Assert.Equal(OrderStatus.Delivered, order!.Status);
    }

    [Fact]
    public async Task AdminUpdateStatus_ReturnsBadRequest_ForInvalidTransition()
    {
        var (customer, address, _) = await SetupReadyToOrderAsync();
        var created = await TestData.CreateOrderAsync(customer, address.Id);
        var admin = await _factory.CreateAdminClientAsync();

        // A new order is Pending, so it cannot jump straight to Delivered.
        var response = await admin.PutAsJsonAsync($"/api/admin/orders/{created.Id}/status", new UpdateOrderStatusRequest
        {
            Status = OrderStatus.Delivered
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AdminUpdateStatus_ReturnsBadRequest_WhenAlreadyDelivered()
    {
        var (customer, address, _) = await SetupReadyToOrderAsync();
        var created = await TestData.CreateOrderAsync(customer, address.Id);
        var admin = await _factory.CreateAdminClientAsync();

        foreach (var status in new[]
                 {
                     OrderStatus.Confirmed,
                     OrderStatus.Processing,
                     OrderStatus.Shipped,
                     OrderStatus.Delivered
                 })
        {
            await admin.PutAsJsonAsync($"/api/admin/orders/{created.Id}/status", new UpdateOrderStatusRequest { Status = status });
        }

        // Delivered is terminal, so it cannot be cancelled.
        var response = await admin.PutAsJsonAsync($"/api/admin/orders/{created.Id}/status", new UpdateOrderStatusRequest
        {
            Status = OrderStatus.Cancelled
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AdminUpdateStatus_ReturnsBadRequest_ForUnknownStatus()
    {
        var (customer, address, _) = await SetupReadyToOrderAsync();
        var created = await TestData.CreateOrderAsync(customer, address.Id);
        var admin = await _factory.CreateAdminClientAsync();

        var response = await admin.PutAsJsonAsync($"/api/admin/orders/{created.Id}/status", new UpdateOrderStatusRequest
        {
            Status = (OrderStatus)99
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AdminUpdateStatus_ReturnsStock_WhenCancelling()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var (customer, address, productId) = await SetupReadyToOrderAsync(admin, quantity: 3, stock: 10);

        await TestData.CreateOrderAsync(customer, address.Id);

        var afterOrder = await admin.GetFromJsonAsync<InventoryResponse>($"/api/inventory/{productId}");
        Assert.Equal(7, afterOrder!.Quantity);

        var page = await admin.GetFromJsonAsync<PagedResult<OrderResponse>>("/api/admin/orders?pageSize=100");
        var order = page!.Items.First(x => x.UserId != 0 && x.Items.Any(i => i.ProductId == productId));

        var response = await admin.PutAsJsonAsync($"/api/admin/orders/{order.Id}/status", new UpdateOrderStatusRequest
        {
            Status = OrderStatus.Cancelled
        });
        response.EnsureSuccessStatusCode();

        var afterCancel = await admin.GetFromJsonAsync<InventoryResponse>($"/api/inventory/{productId}");

        Assert.Equal(10, afterCancel!.Quantity);
    }

    [Fact]
    public async Task AdminUpdateStatus_ReturnsNotFound_ForMissingOrder()
    {
        var admin = await _factory.CreateAdminClientAsync();

        var response = await admin.PutAsJsonAsync("/api/admin/orders/999999/status", new UpdateOrderStatusRequest
        {
            Status = OrderStatus.Confirmed
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CustomerCannotDeleteAnAddressUsedByTheirOwnOrder()
    {
        var (customer, address, _) = await SetupReadyToOrderAsync();
        await TestData.CreateOrderAsync(customer, address.Id);

        var response = await customer.DeleteAsync($"/api/addresses/{address.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
