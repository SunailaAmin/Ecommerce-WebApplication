using ECommerce.Application.DTOs.Payments;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Interfaces;
using ECommerce.Application.Services;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ECommerce.UnitTests.Services;

public class CheckoutServiceTests
{
    private const string ApprovedCard = "4111111111111111";
    private const string DeclinedCard = "4111111111110000";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<ICartRepository> _cartRepository = new();
    private readonly Mock<IAddressRepository> _addressRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IPaymentRepository> _paymentRepository = new();
    private readonly Mock<IPaymentGateway> _paymentGateway = new();
    private readonly Mock<IOrderNotificationPublisher> _notificationPublisher = new();
    private readonly CheckoutService _service;

    public CheckoutServiceTests()
    {
        var orderService = new OrderService(
            _orderRepository.Object,
            _cartRepository.Object,
            _addressRepository.Object,
            _unitOfWork.Object,
            _notificationPublisher.Object,
            NullLogger<OrderService>.Instance);

        var paymentService = new PaymentService(
            _orderRepository.Object,
            _paymentRepository.Object,
            _paymentGateway.Object,
            _unitOfWork.Object,
            _notificationPublisher.Object,
            NullLogger<PaymentService>.Instance);

        _service = new CheckoutService(
            orderService,
            _orderRepository.Object,
            paymentService,
            _unitOfWork.Object,
            NullLogger<CheckoutService>.Instance);

        _orderRepository
            .Setup(x => x.OrderNumberExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _unitOfWork
            .Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWork
            .Setup(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWork
            .Setup(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    public Cart? LastCart { get; private set; }

    private static Product CreateProduct(int productId = 10, decimal price = 50m, int stock = 20, bool isActive = true)
    {
        var product = new Product
        {
            Id = productId,
            Name = "Pro Laptop",
            Sku = "LAP-1",
            Price = price,
            IsActive = isActive
        };

        product.Inventory = new Inventory
        {
            Id = productId,
            ProductId = productId,
            Quantity = stock,
            Product = product
        };

        return product;
    }

    private static Cart CreateCart(int userId, Product product, int quantity)
    {
        var now = DateTime.UtcNow;

        return new Cart
        {
            Id = userId,
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
            Items =
            [
                new CartItem
                {
                    Id = 100,
                    ProductId = product.Id,
                    Quantity = quantity,
                    Product = product
                }
            ]
        };
    }

    private static Address CreateAddress(int userId) => new()
    {
        Id = userId,
        UserId = userId,
        FullName = "Test Recipient",
        AddressLine = "1 Test Street",
        City = "Testville",
        State = "TestState",
        PostalCode = "12345",
        Country = "TestCountry",
        PhoneNumber = "5550100"
    };

    /// <summary>
    /// Gives a user a cart holding one product plus their own address. The address id is
    /// the user id, so two users in the same test never share an address lookup.
    /// </summary>
    private Product SetupCart(int userId, int quantity, Product product)
    {
        var cart = CreateCart(userId, product, quantity);
        LastCart = cart;

        _cartRepository
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);

        _addressRepository
            .Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateAddress(userId));

        return product;
    }

    private Product SetupCartWithOneProduct(int userId = 1, int quantity = 2) =>
        SetupCart(userId, quantity, CreateProduct(productId: userId * 10));

    private static CheckoutRequest Request(int userId, string cardNumber) => new()
    {
        ShippingAddressId = userId,
        CardNumber = cardNumber
    };

    private void SetupApprovedPayment()
    {
        _paymentGateway
            .Setup(x => x.ChargeAsync(It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PaymentResult.Approved("TXN-ABC123"));
    }

    private void SetupDeclinedPayment()
    {
        _paymentGateway
            .Setup(x => x.ChargeAsync(It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PaymentResult.Declined("The card was declined by the payment provider."));
    }

    [Fact]
    public async Task CheckoutAsync_ConfirmsTheOrder_WhenPaymentSucceeds()
    {
        var product = SetupCartWithOneProduct(quantity: 2);
        SetupApprovedPayment();

        var result = await _service.CheckoutAsync(1, Request(1, ApprovedCard), CancellationToken.None);

        Assert.Equal(OrderStatus.Confirmed, result.Order.Status);
        Assert.Equal(PaymentStatus.Successful, result.Payment.Status);
        Assert.Equal("TXN-ABC123", result.Payment.TransactionId);
        Assert.Equal(100m, result.Order.TotalAmount);
        Assert.Equal(result.Order.Id, result.Payment.OrderId);
        Assert.Equal(18, product.Inventory!.Quantity);
    }

    [Fact]
    public async Task CheckoutAsync_KeepsTheOrderPending_WhenPaymentIsDeclined()
    {
        var product = SetupCartWithOneProduct(quantity: 2);
        SetupDeclinedPayment();

        var result = await _service.CheckoutAsync(1, Request(1, DeclinedCard), CancellationToken.None);

        // The order and its stock are kept so the customer can retry with another card.
        Assert.Equal(OrderStatus.Pending, result.Order.Status);
        Assert.Equal(PaymentStatus.Failed, result.Payment.Status);
        Assert.Equal("The card was declined by the payment provider.", result.Payment.FailureReason);
        Assert.Equal(18, product.Inventory!.Quantity);
    }

    [Fact]
    public async Task CheckoutAsync_ClearsTheCartAndReservesTheStock()
    {
        var product = SetupCartWithOneProduct(quantity: 2);
        SetupApprovedPayment();

        await _service.CheckoutAsync(1, Request(1, ApprovedCard), CancellationToken.None);

        Assert.NotNull(LastCart);
        Assert.Empty(LastCart!.Items);
        Assert.Equal(18, product.Inventory!.Quantity);
    }

    [Fact]
    public async Task CheckoutAsync_ThrowsValidation_WhenTheCartIsEmpty()
    {
        var now = DateTime.UtcNow;

        _cartRepository
            .Setup(x => x.GetByUserIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Cart { Id = 1, UserId = 1, CreatedAt = now, UpdatedAt = now, Items = [] });

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CheckoutAsync(1, Request(1, ApprovedCard), CancellationToken.None));

        _paymentGateway.Verify(
            x => x.ChargeAsync(It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CheckoutAsync_RejectsARepeatedSubmission()
    {
        SetupCartWithOneProduct();
        SetupApprovedPayment();

        await _service.CheckoutAsync(1, Request(1, ApprovedCard), CancellationToken.None);

        // The first checkout emptied the cart, so a double submit cannot create a second order.
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CheckoutAsync(1, Request(1, ApprovedCard), CancellationToken.None));

        _orderRepository.Verify(x => x.Add(It.IsAny<Order>()), Times.Once);
    }

    [Fact]
    public async Task CheckoutAsync_RejectsAnAddressOwnedBySomeoneElse()
    {
        SetupCartWithOneProduct();
        SetupApprovedPayment();

        _addressRepository
            .Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateAddress(99));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.CheckoutAsync(1, Request(1, ApprovedCard), CancellationToken.None));

        _orderRepository.Verify(x => x.Add(It.IsAny<Order>()), Times.Never);
    }

    [Fact]
    public async Task CheckoutAsync_RejectsInsufficientStock()
    {
        var product = CreateProduct(stock: 1);
        SetupCart(1, quantity: 5, product);
        SetupApprovedPayment();

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CheckoutAsync(1, Request(1, ApprovedCard), CancellationToken.None));

        _paymentGateway.Verify(
            x => x.ChargeAsync(It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CheckoutAsync_RejectsAnInactiveProduct()
    {
        var product = CreateProduct(isActive: false);
        SetupCart(1, quantity: 1, product);
        SetupApprovedPayment();

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CheckoutAsync(1, Request(1, ApprovedCard), CancellationToken.None));
    }

    [Fact]
    public async Task CheckoutAsync_ServesTwoCustomersInParallel()
    {
        // The per-user gate must not serialise unrelated customers.
        var firstProduct = SetupCartWithOneProduct(userId: 1, quantity: 2);
        var secondProduct = SetupCart(2, quantity: 1, CreateProduct(productId: 20));
        SetupApprovedPayment();

        var results = await Task.WhenAll(
            _service.CheckoutAsync(1, Request(1, ApprovedCard), CancellationToken.None),
            _service.CheckoutAsync(2, Request(2, ApprovedCard), CancellationToken.None));

        Assert.All(results, x => Assert.Equal(OrderStatus.Confirmed, x.Order.Status));
        Assert.Equal(18, firstProduct.Inventory!.Quantity);
        Assert.Equal(19, secondProduct.Inventory!.Quantity);
    }
}
