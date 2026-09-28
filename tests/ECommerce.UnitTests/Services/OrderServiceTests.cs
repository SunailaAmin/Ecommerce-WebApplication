using ECommerce.Application.DTOs.Orders;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Interfaces;
using ECommerce.Application.Notifications;
using ECommerce.Application.Services;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ECommerce.UnitTests.Services;

public class OrderServiceTests
{
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<ICartRepository> _cartRepository = new();
    private readonly Mock<IAddressRepository> _addressRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IOrderNotificationPublisher> _notificationPublisher = new();
    private readonly OrderService _service;

    public OrderServiceTests()
    {
        _service = new OrderService(
            _orderRepository.Object,
            _cartRepository.Object,
            _addressRepository.Object,
            _unitOfWork.Object,
            _notificationPublisher.Object,
            NullLogger<OrderService>.Instance);

        _orderRepository
            .Setup(x => x.OrderNumberExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    private static Inventory CreateInventory(
        int productId = 10,
        decimal price = 50m,
        int stock = 20,
        bool isActive = true)
    {
        var product = new Product
        {
            Id = productId,
            Name = "Pro Laptop",
            Sku = "LAP-1",
            Price = price,
            IsActive = isActive
        };

        var inventory = new Inventory
        {
            Id = productId,
            ProductId = productId,
            Quantity = stock,
            Product = product
        };

        product.Inventory = inventory;

        return inventory;
    }

    private static CartItem CreateCartItem(Product product, int quantity = 1) => new()
    {
        Id = 100,
        ProductId = product.Id,
        Quantity = quantity,
        Product = product
    };

    private static Cart CreateCart(int userId, params CartItem[] items)
    {
        var now = DateTime.UtcNow;

        return new Cart
        {
            Id = 5,
            UserId = userId,
            CreatedAt = now,
            UpdatedAt = now,
            User = new User
            {
                Id = userId,
                FirstName = "Test",
                LastName = "User",
                Email = "test@test.com"
            },
            Items = new List<CartItem>(items)
        };
    }

    private static Address CreateAddress(int userId, int id = 7) => new()
    {
        Id = id,
        UserId = userId,
        FullName = "Test Recipient",
        AddressLine = "1 Test Street",
        City = "Testville",
        State = "TestState",
        PostalCode = "12345",
        Country = "TestCountry",
        PhoneNumber = "5550100"
    };

    private static Order CreateOrder(
        OrderStatus status = OrderStatus.Pending,
        int userId = 1,
        int id = 42,
        Payment? payment = null,
        params OrderItem[] items)
    {
        var now = DateTime.UtcNow;

        var order = new Order
        {
            Id = id,
            OrderNumber = "ORD-20260101-ABC123",
            UserId = userId,
            TotalAmount = items.Sum(x => x.TotalPrice),
            Status = status,
            ShippingAddress = CreateAddress(userId),
            CreatedAt = now,
            UpdatedAt = now,
            User = new User
            {
                Id = userId,
                FirstName = "Test",
                LastName = "User",
                Email = "test@test.com"
            },
            Items = new List<OrderItem>(items),
            Payments = payment is null ? new List<Payment>() : new List<Payment> { payment }
        };

        return order;
    }

    [Fact]
    public async Task CreateOrderAsync_ThrowsValidation_WhenCartIsEmpty()
    {
        _cartRepository
            .Setup(x => x.GetByUserIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCart(1));

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateOrderAsync(1, new CreateOrderRequest { ShippingAddressId = 7 }, CancellationToken.None));
    }

    [Fact]
    public async Task CreateOrderAsync_ThrowsValidation_WhenNoCartExists()
    {
        _cartRepository
            .Setup(x => x.GetByUserIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cart?)null);

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateOrderAsync(1, new CreateOrderRequest { ShippingAddressId = 7 }, CancellationToken.None));
    }

    [Fact]
    public async Task CreateOrderAsync_CalculatesTotalFromProductPrices()
    {
        var first = CreateInventory(productId: 10, price: 50m, stock: 20);
        var second = CreateInventory(productId: 11, price: 25.50m, stock: 20);

        _cartRepository
            .Setup(x => x.GetByUserIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCart(1, CreateCartItem(first.Product, 2), CreateCartItem(second.Product, 4)));

        _addressRepository
            .Setup(x => x.GetByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateAddress(1));

        Order? captured = null;
        _orderRepository
            .Setup(x => x.Add(It.IsAny<Order>()))
            .Callback<Order>(o => captured = o);

        var result = await _service.CreateOrderAsync(1, new CreateOrderRequest { ShippingAddressId = 7 }, CancellationToken.None);

        // 50 * 2 + 25.50 * 4 = 202
        Assert.Equal(202m, result.TotalAmount);
        Assert.Equal(OrderStatus.Pending, result.Status);
        Assert.Equal(2, result.Items.Count);
        Assert.NotNull(captured);
        Assert.StartsWith("ORD-", captured!.OrderNumber);
    }

    [Fact]
    public async Task CreateOrderAsync_SnapshotsUnitPriceOnTheOrderItem()
    {
        var inventory = CreateInventory(productId: 10, price: 50m, stock: 20);

        _cartRepository
            .Setup(x => x.GetByUserIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCart(1, CreateCartItem(inventory.Product, 2)));

        _addressRepository
            .Setup(x => x.GetByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateAddress(1));

        var result = await _service.CreateOrderAsync(1, new CreateOrderRequest { ShippingAddressId = 7 }, CancellationToken.None);

        // The order keeps the price it was placed at even if the product changes later.
        inventory.Product.Price = 999m;

        Assert.Equal(50m, result.Items[0].UnitPrice);
        Assert.Equal(100m, result.Items[0].TotalPrice);
    }

    [Fact]
    public async Task CreateOrderAsync_DecrementsInventory()
    {
        var inventory = CreateInventory(productId: 10, price: 50m, stock: 20);

        _cartRepository
            .Setup(x => x.GetByUserIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCart(1, CreateCartItem(inventory.Product, 3)));

        _addressRepository
            .Setup(x => x.GetByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateAddress(1));

        await _service.CreateOrderAsync(1, new CreateOrderRequest { ShippingAddressId = 7 }, CancellationToken.None);

        Assert.Equal(17, inventory.Quantity);
    }

    [Fact]
    public async Task CreateOrderAsync_EmptiesTheCart()
    {
        var inventory = CreateInventory();

        _cartRepository
            .Setup(x => x.GetByUserIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCart(1, CreateCartItem(inventory.Product, 1)));

        _addressRepository
            .Setup(x => x.GetByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateAddress(1));

        await _service.CreateOrderAsync(1, new CreateOrderRequest { ShippingAddressId = 7 }, CancellationToken.None);

        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateOrderAsync_ThrowsNotFound_WhenAddressBelongsToAnotherCustomer()
    {
        var inventory = CreateInventory();

        _cartRepository
            .Setup(x => x.GetByUserIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCart(1, CreateCartItem(inventory.Product, 1)));

        _addressRepository
            .Setup(x => x.GetByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateAddress(userId: 2));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.CreateOrderAsync(1, new CreateOrderRequest { ShippingAddressId = 7 }, CancellationToken.None));
    }

    [Fact]
    public async Task CreateOrderAsync_ThrowsNotFound_WhenAddressDoesNotExist()
    {
        var inventory = CreateInventory();

        _cartRepository
            .Setup(x => x.GetByUserIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCart(1, CreateCartItem(inventory.Product, 1)));

        _addressRepository
            .Setup(x => x.GetByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Address?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.CreateOrderAsync(1, new CreateOrderRequest { ShippingAddressId = 7 }, CancellationToken.None));
    }

    [Fact]
    public async Task CreateOrderAsync_ThrowsValidation_WhenStockInsufficient()
    {
        var inventory = CreateInventory(productId: 10, price: 50m, stock: 2);

        _cartRepository
            .Setup(x => x.GetByUserIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCart(1, CreateCartItem(inventory.Product, 5)));

        _addressRepository
            .Setup(x => x.GetByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateAddress(1));

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateOrderAsync(1, new CreateOrderRequest { ShippingAddressId = 7 }, CancellationToken.None));

        Assert.Contains("Pro Laptop", exception.Message);
        Assert.Equal(2, inventory.Quantity);
    }

    [Fact]
    public async Task CreateOrderAsync_ThrowsValidation_WhenProductInactive()
    {
        var inventory = CreateInventory(isActive: false);

        _cartRepository
            .Setup(x => x.GetByUserIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCart(1, CreateCartItem(inventory.Product, 1)));

        _addressRepository
            .Setup(x => x.GetByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateAddress(1));

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateOrderAsync(1, new CreateOrderRequest { ShippingAddressId = 7 }, CancellationToken.None));
    }

    [Fact]
    public async Task CreateOrderAsync_ReturnsCustomerDetails()
    {
        var inventory = CreateInventory();

        _cartRepository
            .Setup(x => x.GetByUserIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCart(1, CreateCartItem(inventory.Product, 1)));

        _addressRepository
            .Setup(x => x.GetByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateAddress(1));

        var result = await _service.CreateOrderAsync(1, new CreateOrderRequest { ShippingAddressId = 7 }, CancellationToken.None);

        Assert.Equal("Test User", result.CustomerName);
        Assert.Equal("test@test.com", result.CustomerEmail);
        Assert.Equal(7, result.ShippingAddress.Id);
    }

    [Fact]
    public async Task GetOrderByIdAsync_ReturnsOrder_WhenCustomerOwnsIt()
    {
        var inventory = CreateInventory();
        _orderRepository
            .Setup(x => x.GetByIdAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOrder(userId: 1, items: new[]
            {
                new OrderItem { Id = 1, ProductId = 10, Quantity = 2, UnitPrice = 50m, TotalPrice = 100m, Product = inventory.Product }
            }));

        var result = await _service.GetOrderByIdAsync(1, 42, isAdmin: false, CancellationToken.None);

        Assert.Equal(42, result.Id);
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task GetOrderByIdAsync_ThrowsNotFound_WhenOrderBelongsToAnotherCustomer()
    {
        _orderRepository
            .Setup(x => x.GetByIdAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOrder(userId: 2));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.GetOrderByIdAsync(1, 42, isAdmin: false, CancellationToken.None));
    }

    [Fact]
    public async Task GetOrderByIdAsync_ReturnsOrder_ForAdminOfAnotherCustomer()
    {
        _orderRepository
            .Setup(x => x.GetByIdAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOrder(userId: 2));

        var result = await _service.GetOrderByIdAsync(99, 42, isAdmin: true, CancellationToken.None);

        Assert.Equal(42, result.Id);
    }

    [Fact]
    public async Task GetOrderByIdAsync_ThrowsNotFound_WhenOrderMissing()
    {
        _orderRepository
            .Setup(x => x.GetByIdAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.GetOrderByIdAsync(1, 42, isAdmin: false, CancellationToken.None));
    }

    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Pending, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Processing)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Processing, OrderStatus.Shipped)]
    [InlineData(OrderStatus.Processing, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Delivered)]
    public async Task UpdateStatusAsync_AllowsValidTransitions(OrderStatus from, OrderStatus to)
    {
        _orderRepository
            .Setup(x => x.GetByIdAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOrder(status: from));

        var result = await _service.UpdateStatusAsync(42, new UpdateOrderStatusRequest { Status = to }, CancellationToken.None);

        Assert.Equal(to, result.Status);
    }

    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Delivered)]
    [InlineData(OrderStatus.Pending, OrderStatus.Processing)]
    [InlineData(OrderStatus.Pending, OrderStatus.Shipped)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Delivered)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Delivered, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Delivered, OrderStatus.Delivered)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Cancelled)]
    public async Task UpdateStatusAsync_RejectsInvalidTransitions(OrderStatus from, OrderStatus to)
    {
        _orderRepository
            .Setup(x => x.GetByIdAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOrder(status: from));

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.UpdateStatusAsync(42, new UpdateOrderStatusRequest { Status = to }, CancellationToken.None));

        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateStatusAsync_ReturnsStock_WhenCancelling()
    {
        var inventory = CreateInventory(productId: 10, price: 50m, stock: 17);
        var order = CreateOrder(
            status: OrderStatus.Processing,
            items: new[]
            {
                new OrderItem { Id = 1, ProductId = 10, Quantity = 3, UnitPrice = 50m, TotalPrice = 150m, Product = inventory.Product }
            });

        _orderRepository
            .Setup(x => x.GetByIdAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var result = await _service.UpdateStatusAsync(42, new UpdateOrderStatusRequest { Status = OrderStatus.Cancelled }, CancellationToken.None);

        Assert.Equal(OrderStatus.Cancelled, result.Status);
        Assert.Equal(20, inventory.Quantity);
    }

    [Fact]
    public async Task UpdateStatusAsync_DoesNotRestock_WhenNotCancelling()
    {
        var inventory = CreateInventory(productId: 10, price: 50m, stock: 17);
        var order = CreateOrder(
            status: OrderStatus.Processing,
            items: new[]
            {
                new OrderItem { Id = 1, ProductId = 10, Quantity = 3, UnitPrice = 50m, TotalPrice = 150m, Product = inventory.Product }
            });

        _orderRepository
            .Setup(x => x.GetByIdAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        await _service.UpdateStatusAsync(42, new UpdateOrderStatusRequest { Status = OrderStatus.Shipped }, CancellationToken.None);

        Assert.Equal(17, inventory.Quantity);
    }

    [Fact]
    public async Task UpdateStatusAsync_ThrowsNotFound_WhenOrderMissing()
    {
        _orderRepository
            .Setup(x => x.GetByIdAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.UpdateStatusAsync(42, new UpdateOrderStatusRequest { Status = OrderStatus.Confirmed }, CancellationToken.None));
    }

    [Fact]
    public async Task GenerateOrderNumber_Retries_WhenNumberAlreadyExists()
    {
        var attempts = 0;

        _orderRepository
            .Setup(x => x.OrderNumberExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                attempts++;
                return attempts <= 2;
            });

        var inventory = CreateInventory();

        _cartRepository
            .Setup(x => x.GetByUserIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCart(1, CreateCartItem(inventory.Product, 1)));

        _addressRepository
            .Setup(x => x.GetByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateAddress(1));

        var result = await _service.CreateOrderAsync(1, new CreateOrderRequest { ShippingAddressId = 7 }, CancellationToken.None);

        Assert.Equal(3, attempts);
        Assert.StartsWith("ORD-", result.OrderNumber);
    }

    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Processing)]
    [InlineData(OrderStatus.Processing, OrderStatus.Shipped)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Delivered)]
    public async Task UpdateStatusAsync_PublishesToTheOrdersOwnCustomer(OrderStatus from, OrderStatus to)
    {
        var order = CreateOrder(from, userId: 77);

        _orderRepository
            .Setup(x => x.GetByIdAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        await _service.UpdateStatusAsync(42, new UpdateOrderStatusRequest { Status = to }, CancellationToken.None);

        _notificationPublisher.Verify(
            x => x.PublishAsync(
                It.Is<OrderNotification>(n => n.UserId == 77 && n.Status == to && n.OrderNumber == order.OrderNumber),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateStatusAsync_StillSucceeds_WhenTheNotificationCannotBeQueued()
    {
        var order = CreateOrder(OrderStatus.Confirmed, userId: 77);

        _orderRepository
            .Setup(x => x.GetByIdAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        _notificationPublisher
            .Setup(x => x.PublishAsync(It.IsAny<OrderNotification>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("queue unavailable"));

        var result = await _service.UpdateStatusAsync(
            42,
            new UpdateOrderStatusRequest { Status = OrderStatus.Processing },
            CancellationToken.None);

        // The status change is already saved, so a notification problem must not turn a
        // successful admin action into a failed request.
        Assert.Equal(OrderStatus.Processing, result.Status);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelAbandonedOrdersAsync_CancelsOldPendingOrdersAndRestocks()
    {
        var product = new Product { Id = 10, Sku = "LAP-1", Price = 50m, IsActive = true };
        var inventory = new Inventory { Id = 10, ProductId = 10, Quantity = 4, Product = product };
        product.Inventory = inventory;

        var order = CreateOrder(
            OrderStatus.Pending,
            9,
            42,
            items:
            [
                new OrderItem
                {
                    Id = 1,
                    ProductId = 10,
                    Quantity = 3,
                    UnitPrice = 50m,
                    TotalPrice = 150m,
                    Product = product
                }
            ]);

        _orderRepository
            .Setup(x => x.GetPendingCreatedBeforeAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Order> { order });

        var cancelled = await _service.CancelAbandonedOrdersAsync(DateTime.UtcNow, CancellationToken.None);

        Assert.Equal(1, cancelled);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal(7, inventory.Quantity);
    }

    [Fact]
    public async Task CancelAbandonedOrdersAsync_DoesNothingWhenThereIsNothingToClean()
    {
        _orderRepository
            .Setup(x => x.GetPendingCreatedBeforeAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Order>());

        var cancelled = await _service.CancelAbandonedOrdersAsync(DateTime.UtcNow, CancellationToken.None);

        Assert.Equal(0, cancelled);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(PaymentStatus.Pending)]
    [InlineData(PaymentStatus.Failed)]
    public async Task UpdateStatusAsync_CancelsUnsettledPayments_WhenOrderIsCancelled(PaymentStatus paymentStatus)
    {
        var payment = CreatePayment(paymentStatus);
        var order = CreateOrder(status: OrderStatus.Pending, id: 42, payment: payment);

        _orderRepository
            .Setup(x => x.GetByIdAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        await _service.UpdateStatusAsync(42, new UpdateOrderStatusRequest { Status = OrderStatus.Cancelled }, CancellationToken.None);

        Assert.Equal(PaymentStatus.Cancelled, payment.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_LeavesASuccessfulPaymentAlone_WhenOrderIsCancelled()
    {
        // The money was taken and the mock gateway cannot refund it, so reporting the
        // payment as Cancelled would hide a charge that really happened.
        var payment = CreatePayment(PaymentStatus.Successful);
        var order = CreateOrder(status: OrderStatus.Confirmed, id: 42, payment: payment);

        _orderRepository
            .Setup(x => x.GetByIdAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        await _service.UpdateStatusAsync(42, new UpdateOrderStatusRequest { Status = OrderStatus.Cancelled }, CancellationToken.None);

        Assert.Equal(PaymentStatus.Successful, payment.Status);
    }

    [Fact]
    public async Task CancelAbandonedOrdersAsync_CancelsUnsettledPayments()
    {
        var payment = CreatePayment(PaymentStatus.Failed);
        var order = CreateOrder(status: OrderStatus.Pending, id: 42, payment: payment);

        _orderRepository
            .Setup(x => x.GetPendingCreatedBeforeAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Order> { order });

        await _service.CancelAbandonedOrdersAsync(DateTime.UtcNow, CancellationToken.None);

        Assert.Equal(PaymentStatus.Cancelled, payment.Status);
    }

    private static Payment CreatePayment(PaymentStatus status) => new()
    {
        Id = 900,
        OrderId = 42,
        Amount = 10m,
        Status = status,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}
