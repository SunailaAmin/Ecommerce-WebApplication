using System.Collections.Concurrent;
using ECommerce.Orders.Application.DTOs.Payments;
using ECommerce.Orders.Application.Interfaces;
using ECommerce.Orders.Application.Mapping;
using ECommerce.Shared.Data;
using Microsoft.Extensions.Logging;
using ECommerce.Shared.Enums;

namespace ECommerce.Orders.Application.Services;

/// <summary>
/// Turns the current cart into a paid order: the cart becomes a Pending order, the
/// payment is attempted, and a successful payment confirms the order.
/// </summary>
public class CheckoutService
{
    // Two checkout requests for the same user arriving together would both read the same
    // cart and produce two orders, so checkout is serialised per user.
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> CheckoutGates = new();

    private readonly OrderService _orderService;
    private readonly IOrderRepository _orderRepository;
    private readonly PaymentService _paymentService;
    private readonly IProductServiceClient _productClient;
    private readonly ILogger<CheckoutService> _logger;

    public CheckoutService(
        OrderService orderService,
        IOrderRepository orderRepository,
        PaymentService paymentService,
        IProductServiceClient productClient,
        ILogger<CheckoutService> logger)
    {
        _orderService = orderService;
        _orderRepository = orderRepository;
        _paymentService = paymentService;
        _productClient = productClient;
        _logger = logger;
    }

    public async Task<CheckoutResponse> CheckoutAsync(
        int userId,
        CheckoutRequest request,
        CancellationToken cancellationToken)
    {
        var gate = CheckoutGates.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));

        await gate.WaitAsync(cancellationToken);

        try
        {
            return await PlaceOrderAsync(userId, request, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<CheckoutResponse> PlaceOrderAsync(
        int userId,
        CheckoutRequest request,
        CancellationToken cancellationToken)
    {
        // The cart is converted into the order here, which is also the duplicate-submission
        // guard: a second checkout finds the cart empty and is rejected. The order and the
        // emptied cart are committed together with the stock reservation, and the order is
        // committed before the charge so a declined card still leaves a retryable order.
        // The reservation lives in Product's database, so it is taken after the order write
        // with a compensating release if it is refused, not as part of one big transaction.
        var order = await _orderService.PrepareOrderFromCartAsync(
            userId,
            request.ShippingAddressId,
            cancellationToken);

        _orderRepository.Add(order);

        await _orderService.SaveAndReserveStockAsync(order, cancellationToken);

        var payment = await _paymentService.PayOrderAsync(order, request.CardNumber, cancellationToken);

        _logger.LogInformation(
            "Checkout completed for order {OrderNumber} with payment status {PaymentStatus}",
            order.OrderNumber,
            payment.Status);

        return new CheckoutResponse(
            await order.ToResponseAsync(_productClient, cancellationToken),
            payment);
    }
}
