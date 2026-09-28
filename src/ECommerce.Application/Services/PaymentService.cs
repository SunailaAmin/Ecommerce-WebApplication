using ECommerce.Application.DTOs.Payments;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Interfaces;
using ECommerce.Application.Mapping;
using ECommerce.Application.Notifications;
using ECommerce.Application.Transactions;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace ECommerce.Application.Services;

/// <summary>
/// Owns charging an order. Checkout and the standalone payment endpoint both go through
/// here, so the two paths cannot drift apart.
/// </summary>
public class PaymentService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOrderNotificationPublisher _notificationPublisher;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        IOrderRepository orderRepository,
        IPaymentRepository paymentRepository,
        IPaymentGateway paymentGateway,
        IUnitOfWork unitOfWork,
        IOrderNotificationPublisher notificationPublisher,
        ILogger<PaymentService> logger)
    {
        _orderRepository = orderRepository;
        _paymentRepository = paymentRepository;
        _paymentGateway = paymentGateway;
        _unitOfWork = unitOfWork;
        _notificationPublisher = notificationPublisher;
        _logger = logger;
    }

    public async Task<PaymentResponse> PayOrderAsync(Order order, string cardNumber, CancellationToken cancellationToken)
    {
        if (order.Status != OrderStatus.Pending)
        {
            throw new ConflictException($"Order {order.OrderNumber} is not awaiting payment.");
        }

        var now = DateTime.UtcNow;

        var payment = new Payment
        {
            OrderId = order.Id,
            Amount = order.TotalAmount,
            Status = PaymentStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };

        _paymentRepository.Add(payment);

        // The charge is made outside a transaction on purpose: a database transaction held
        // open across a payment provider call would lock the order and inventory rows for
        // the whole round trip. A declined payment therefore still commits, and the order
        // stays Pending so the customer can retry with another card.
        var result = await ChargeAsync(order, payment.Amount, cardNumber, cancellationToken);

        var settledAt = DateTime.UtcNow;
        payment.UpdatedAt = settledAt;

        if (result.Succeeded)
        {
            payment.Status = PaymentStatus.Successful;
            payment.TransactionId = result.TransactionId;

            order.Status = OrderStatus.Confirmed;
            order.UpdatedAt = settledAt;

            _logger.LogInformation(
                "Payment {TransactionId} succeeded for order {OrderNumber}",
                result.TransactionId,
                order.OrderNumber);
        }
        else
        {
            payment.Status = PaymentStatus.Failed;
            payment.FailureReason = result.FailureReason;

            _logger.LogWarning(
                "Payment failed for order {OrderNumber}: {FailureReason}",
                order.OrderNumber,
                result.FailureReason);
        }

        await _unitOfWork.CommitChangesInTransactionAsync(cancellationToken);

        if (result.Succeeded)
        {
            await PublishOrderConfirmedAsync(order, cancellationToken);
        }

        return payment.ToResponse(order.OrderNumber);
    }

    /// <summary>
    /// A successful payment is what moves the order to Confirmed, so the confirmation
    /// notice is raised here rather than by checkout.
    /// </summary>
    private async Task PublishOrderConfirmedAsync(Order order, CancellationToken cancellationToken)
    {
        var notification = new OrderNotification(
            order.Id,
            order.OrderNumber,
            order.UserId,
            OrderStatus.Confirmed,
            $"Order {order.OrderNumber} is confirmed.",
            DateTime.UtcNow);

        try
        {
            await _notificationPublisher.PublishAsync(notification, cancellationToken);
        }
        catch (Exception ex)
        {
            // The payment is already committed, so a queueing failure must not turn a
            // successful charge into a failed request.
            _logger.LogWarning(
                ex,
                "Could not queue the confirmation notification for order {OrderNumber}",
                order.OrderNumber);
        }
    }

    public async Task<PaymentResponse> PayExistingOrderAsync(
        int userId,
        PaymentRequest request,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(request.OrderId, cancellationToken);

        // Reported as not found rather than forbidden, so a customer cannot learn that
        // another customer's order exists.
        if (order is null || (!isAdmin && order.UserId != userId))
        {
            throw new NotFoundException($"Order with id {request.OrderId} was not found.");
        }

        return await PayOrderAsync(order, request.CardNumber, cancellationToken);
    }

    public async Task<PaymentResponse> GetByIdAsync(
        int userId,
        int paymentId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        var payment = await _paymentRepository.GetByIdAsync(paymentId, cancellationToken);

        if (payment is null || (!isAdmin && payment.Order.UserId != userId))
        {
            throw new NotFoundException($"Payment with id {paymentId} was not found.");
        }

        return payment.ToResponse(payment.Order.OrderNumber);
    }

    /// <summary>
    /// The most recent payment attempt on one of the caller's orders. Looked up by order
    /// rather than by payment id because the storefront only holds the order id, and an
    /// order can have several attempts after a decline. An order that has not been paid
    /// for yet has no payment to return, which is reported the same way as an unknown
    /// order so the response cannot be used to probe for other people's orders.
    /// </summary>
    public async Task<PaymentResponse> GetForOrderAsync(
        int userId,
        int orderId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken);

        if (order is null || (!isAdmin && order.UserId != userId))
        {
            throw new NotFoundException($"Order with id {orderId} was not found.");
        }

        var payment = await _paymentRepository.GetLatestForOrderAsync(orderId, cancellationToken);

        if (payment is null)
        {
            throw new NotFoundException($"No payment has been attempted for order {orderId}.");
        }

        return payment.ToResponse(order.OrderNumber);
    }

    private async Task<PaymentResult> ChargeAsync(
        Order order,
        decimal amount,
        string cardNumber,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _paymentGateway.ChargeAsync(amount, cardNumber, cancellationToken);
        }
        catch (Exception ex)
        {
            // The outcome is unknown when the provider cannot be reached, so the order is
            // left untouched and Pending, which keeps a retry possible.
            _logger.LogError(ex, "Payment provider call failed for order {OrderNumber}", order.OrderNumber);

            return PaymentResult.Declined("The payment provider could not be reached. Please try again.");
        }
    }
}
