using ECommerce.Application.DTOs.Payments;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Interfaces;
using ECommerce.Application.Notifications;
using ECommerce.Application.Services;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ECommerce.UnitTests.Services;

public class PaymentServiceTests
{
    private const string ApprovedCard = "4111111111111111";
    private const string DeclinedCard = "4111111111110000";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IPaymentRepository> _paymentRepository = new();
    private readonly Mock<IPaymentGateway> _paymentGateway = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IOrderNotificationPublisher> _notificationPublisher = new();
    private readonly List<string> _calls = [];
    private readonly PaymentService _service;

    public PaymentServiceTests()
    {
        _service = new PaymentService(
            _orderRepository.Object,
            _paymentRepository.Object,
            _paymentGateway.Object,
            _unitOfWork.Object,
            _notificationPublisher.Object,
            NullLogger<PaymentService>.Instance);

        // The order of these calls is the point of several tests below, so they are logged
        // rather than only verified.
        _unitOfWork
            .Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("begin"))
            .Returns(Task.CompletedTask);

        _unitOfWork
            .Setup(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("commit"))
            .Returns(Task.CompletedTask);

        _unitOfWork
            .Setup(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("rollback"))
            .Returns(Task.CompletedTask);

        _paymentRepository
            .Setup(x => x.Add(It.IsAny<Payment>()))
            .Callback<Payment>(p => CapturedPayment = p);
    }

    public Payment? CapturedPayment { get; private set; }

    private static Order CreateOrder(
        OrderStatus status = OrderStatus.Pending,
        int userId = 1,
        int id = 42,
        decimal totalAmount = 100m)
    {
        var now = DateTime.UtcNow;

        return new Order
        {
            Id = id,
            OrderNumber = "ORD-20260101-ABC123",
            UserId = userId,
            TotalAmount = totalAmount,
            Status = status,
            CreatedAt = now,
            UpdatedAt = now,
            User = new User
            {
                Id = userId,
                FirstName = "Test",
                LastName = "User",
                Email = "test@test.com"
            },
            ShippingAddress = new Address
            {
                Id = 7,
                UserId = userId,
                FullName = "Test Recipient",
                AddressLine = "1 Test Street",
                City = "Testville",
                State = "TestState",
                PostalCode = "12345",
                Country = "TestCountry"
            }
        };
    }

    private void SetupApprovedPayment()
    {
        _paymentGateway
            .Setup(x => x.ChargeAsync(It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("charge"))
            .ReturnsAsync(PaymentResult.Approved("TXN-ABC123"));
    }

    private void SetupDeclinedPayment()
    {
        _paymentGateway
            .Setup(x => x.ChargeAsync(It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("charge"))
            .ReturnsAsync(PaymentResult.Declined("The card was declined by the payment provider."));
    }

    [Fact]
    public async Task PayOrderAsync_ConfirmsOrder_WhenPaymentSucceeds()
    {
        SetupApprovedPayment();

        var order = CreateOrder();

        var result = await _service.PayOrderAsync(order, ApprovedCard, CancellationToken.None);

        Assert.Equal(PaymentStatus.Successful, result.Status);
        Assert.Equal("TXN-ABC123", result.TransactionId);
        Assert.Null(result.FailureReason);
        Assert.Equal(OrderStatus.Confirmed, order.Status);
    }

    [Fact]
    public async Task PayOrderAsync_KeepsOrderPending_WhenPaymentIsDeclined()
    {
        SetupDeclinedPayment();

        var order = CreateOrder();

        var result = await _service.PayOrderAsync(order, DeclinedCard, CancellationToken.None);

        // A declined payment must stay retryable, so the order is not cancelled here.
        Assert.Equal(PaymentStatus.Failed, result.Status);
        Assert.Equal("The card was declined by the payment provider.", result.FailureReason);
        Assert.Null(result.TransactionId);
        Assert.Equal(OrderStatus.Pending, order.Status);
    }

    [Fact]
    public async Task PayOrderAsync_ChargesTheOrderTotal_AndIgnoresAnyClientAmount()
    {
        SetupApprovedPayment();

        await _service.PayOrderAsync(
            CreateOrder(totalAmount: 249.99m),
            ApprovedCard,
            CancellationToken.None);

        _paymentGateway.Verify(
            x => x.ChargeAsync(249.99m, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PayOrderAsync_ChargesOutsideTheTransaction()
    {
        SetupApprovedPayment();

        await _service.PayOrderAsync(CreateOrder(), ApprovedCard, CancellationToken.None);

        // Holding a transaction open across a provider call would lock the order and
        // inventory rows for the whole round trip, so the charge runs first.
        Assert.Equal(["charge", "begin", "commit"], _calls);
    }

    [Fact]
    public async Task PayOrderAsync_RecordsFailure_WhenGatewayThrows()
    {
        _paymentGateway
            .Setup(x => x.ChargeAsync(It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("charge"))
            .ThrowsAsync(new HttpRequestException("gateway unreachable"));

        var order = CreateOrder();

        var result = await _service.PayOrderAsync(order, ApprovedCard, CancellationToken.None);

        Assert.Equal(PaymentStatus.Failed, result.Status);
        Assert.Equal("The payment provider could not be reached. Please try again.", result.FailureReason);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.DoesNotContain("rollback", _calls);
    }

    [Fact]
    public async Task PayOrderAsync_RollsBack_WhenTheCommitFails()
    {
        SetupApprovedPayment();

        _unitOfWork
            .Setup(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("commit"))
            .ThrowsAsync(new InvalidOperationException("write failed"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.PayOrderAsync(CreateOrder(), ApprovedCard, CancellationToken.None));

        Assert.Contains("rollback", _calls);
    }

    [Theory]
    [InlineData(OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Processing)]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Delivered)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task PayOrderAsync_ThrowsConflict_WhenOrderIsNotAwaitingPayment(OrderStatus status)
    {
        SetupApprovedPayment();

        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.PayOrderAsync(CreateOrder(status), ApprovedCard, CancellationToken.None));

        _paymentGateway.Verify(
            x => x.ChargeAsync(It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PayOrderAsync_DoesNotStoreTheCardNumber()
    {
        SetupApprovedPayment();

        await _service.PayOrderAsync(CreateOrder(), ApprovedCard, CancellationToken.None);

        Assert.NotNull(CapturedPayment);
        Assert.Equal(PaymentStatus.Successful, CapturedPayment!.Status);

        // The card number is passed to the provider and then forgotten, so the saved
        // record can only ever hold the outcome.
        Assert.DoesNotContain(
            typeof(Payment).GetProperties(),
            p => p.Name.Contains("Card", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PayExistingOrderAsync_PaysTheCustomersOwnOrder()
    {
        SetupApprovedPayment();

        _orderRepository
            .Setup(x => x.GetByIdAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOrder(userId: 1));

        var result = await _service.PayExistingOrderAsync(
            1,
            new PaymentRequest { OrderId = 42, CardNumber = ApprovedCard },
            isAdmin: false,
            CancellationToken.None);

        Assert.Equal(PaymentStatus.Successful, result.Status);
    }

    [Fact]
    public async Task PayExistingOrderAsync_ThrowsNotFound_ForAnotherCustomersOrder()
    {
        _orderRepository
            .Setup(x => x.GetByIdAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOrder(userId: 2));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.PayExistingOrderAsync(
                1,
                new PaymentRequest { OrderId = 42, CardNumber = ApprovedCard },
                isAdmin: false,
                CancellationToken.None));

        _paymentGateway.Verify(
            x => x.ChargeAsync(It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PayExistingOrderAsync_ThrowsNotFound_WhenOrderDoesNotExist()
    {
        _orderRepository
            .Setup(x => x.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.PayExistingOrderAsync(
                1,
                new PaymentRequest { OrderId = 999, CardNumber = ApprovedCard },
                isAdmin: false,
                CancellationToken.None));
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsTheOwnersPayment()
    {
        var order = CreateOrder(userId: 1);
        order.Payments = [];

        _paymentRepository
            .Setup(x => x.GetByIdAsync(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Payment
            {
                Id = 9,
                OrderId = order.Id,
                Amount = 100m,
                Status = PaymentStatus.Successful,
                TransactionId = "TXN-ABC123",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Order = order
            });

        var result = await _service.GetByIdAsync(1, 9, isAdmin: false, CancellationToken.None);

        Assert.Equal(9, result.Id);
        Assert.Equal("ORD-20260101-ABC123", result.OrderNumber);
        Assert.Equal(PaymentStatus.Successful, result.Status);
    }

    [Fact]
    public async Task GetByIdAsync_ThrowsNotFound_ForAnotherCustomersPayment()
    {
        var order = CreateOrder(userId: 2);
        order.Payments = [];

        _paymentRepository
            .Setup(x => x.GetByIdAsync(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Payment { Id = 9, OrderId = order.Id, Order = order });

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.GetByIdAsync(1, 9, isAdmin: false, CancellationToken.None));
    }

    [Fact]
    public async Task GetByIdAsync_AllowsAnAdminToReadAnyPayment()
    {
        var order = CreateOrder(userId: 2);
        order.Payments = [];

        _paymentRepository
            .Setup(x => x.GetByIdAsync(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Payment
            {
                Id = 9,
                OrderId = order.Id,
                Amount = 100m,
                Status = PaymentStatus.Failed,
                FailureReason = "declined",
                Order = order
            });

        var result = await _service.GetByIdAsync(1, 9, isAdmin: true, CancellationToken.None);

        Assert.Equal(PaymentStatus.Failed, result.Status);
        Assert.Equal("declined", result.FailureReason);
    }

    [Fact]
    public async Task GetByIdAsync_ThrowsNotFound_WhenPaymentDoesNotExist()
    {
        _paymentRepository
            .Setup(x => x.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.GetByIdAsync(1, 999, isAdmin: true, CancellationToken.None));
    }

    [Fact]
    public async Task PayOrderAsync_PublishesAConfirmation_WhenTheChargeSucceeds()
    {
        var order = CreateOrder(userId: 31);

        SetupApprovedPayment();

        await _service.PayOrderAsync(order, ApprovedCard, CancellationToken.None);

        _notificationPublisher.Verify(
            x => x.PublishAsync(
                It.Is<OrderNotification>(n =>
                    n.UserId == 31
                    && n.Status == OrderStatus.Confirmed
                    && n.OrderNumber == order.OrderNumber),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PayOrderAsync_PublishesNothing_WhenTheChargeIsDeclined()
    {
        var order = CreateOrder(userId: 31);

        SetupDeclinedPayment();

        await _service.PayOrderAsync(order, DeclinedCard, CancellationToken.None);

        // A declined order stays Pending, so there is no status change to announce.
        Assert.Equal(OrderStatus.Pending, order.Status);
        _notificationPublisher.Verify(
            x => x.PublishAsync(It.IsAny<OrderNotification>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PayOrderAsync_StillSucceeds_WhenTheNotificationCannotBeQueued()
    {
        var order = CreateOrder(userId: 31);

        SetupApprovedPayment();

        _notificationPublisher
            .Setup(x => x.PublishAsync(It.IsAny<OrderNotification>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("queue unavailable"));

        var payment = await _service.PayOrderAsync(order, ApprovedCard, CancellationToken.None);

        // The charge is already committed, so a notification problem must not turn a
        // successful payment into a failed request.
        Assert.Equal(PaymentStatus.Successful, payment.Status);
    }

    [Fact]
    public async Task GetForOrderAsync_ReturnsTheLatestPaymentForTheOrder()
    {
        var order = CreateOrder(userId: 5);

        _orderRepository
            .Setup(x => x.GetByIdAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        _paymentRepository
            .Setup(x => x.GetLatestForOrderAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Payment
            {
                Id = 77,
                OrderId = 42,
                Amount = 25m,
                Status = PaymentStatus.Failed,
                FailureReason = "The card was declined by the payment provider.",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

        var result = await _service.GetForOrderAsync(5, 42, isAdmin: false, CancellationToken.None);

        Assert.Equal(77, result.Id);
        Assert.Equal(order.OrderNumber, result.OrderNumber);
        Assert.Equal(PaymentStatus.Failed, result.Status);
        Assert.Equal("The card was declined by the payment provider.", result.FailureReason);
    }

    [Fact]
    public async Task GetForOrderAsync_ThrowsNotFound_WhenTheOrderHasNoPaymentYet()
    {
        _orderRepository
            .Setup(x => x.GetByIdAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOrder(userId: 5));

        _paymentRepository
            .Setup(x => x.GetLatestForOrderAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.GetForOrderAsync(5, 42, isAdmin: false, CancellationToken.None));
    }

    [Fact]
    public async Task GetForOrderAsync_ThrowsNotFound_ForAnotherCustomersOrder()
    {
        // The same answer as an unknown order, so the response cannot be used to discover
        // that somebody else's order exists.
        _orderRepository
            .Setup(x => x.GetByIdAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOrder(userId: 99));

        _paymentRepository
            .Setup(x => x.GetLatestForOrderAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Payment { Id = 77, OrderId = 42, Amount = 25m });

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.GetForOrderAsync(5, 42, isAdmin: false, CancellationToken.None));

        _paymentRepository.Verify(
            x => x.GetLatestForOrderAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetForOrderAsync_ThrowsNotFound_ForAnUnknownOrder()
    {
        _orderRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.GetForOrderAsync(5, 42, isAdmin: false, CancellationToken.None));
    }
}
