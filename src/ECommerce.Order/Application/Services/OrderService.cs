using ECommerce.Orders.Application.DTOs;
using ECommerce.Orders.Application.DTOs.Orders;
using ECommerce.Orders.Application.DTOs.Products;
using ECommerce.Shared.Exceptions;
using ECommerce.Orders.Application.Interfaces;
using ECommerce.Orders.Application.Mapping;
using ECommerce.Orders.Application.Notifications;
using ECommerce.Orders.Domain.Entities;
using ECommerce.Orders.Domain.Enums;
using Microsoft.Extensions.Logging;
using ECommerce.Shared.Data;
using ECommerce.Shared.DTOs;
using ECommerce.Shared.Enums;

namespace ECommerce.Orders.Application.Services;

public class OrderService
{
    private const int OrderNumberAttempts = 5;

    /// <summary>
    /// The only permitted status changes. Delivered and Cancelled are terminal, and an
    /// order can no longer be cancelled once it has been shipped.
    /// </summary>
    private static readonly Dictionary<OrderStatus, OrderStatus[]> AllowedTransitions = new()
    {
        [OrderStatus.Pending] = [OrderStatus.Confirmed, OrderStatus.Cancelled],
        [OrderStatus.Confirmed] = [OrderStatus.Processing, OrderStatus.Cancelled],
        [OrderStatus.Processing] = [OrderStatus.Shipped, OrderStatus.Cancelled],
        [OrderStatus.Shipped] = [OrderStatus.Delivered],
        [OrderStatus.Delivered] = [],
        [OrderStatus.Cancelled] = []
    };

    private readonly IOrderRepository _orderRepository;
    private readonly ICartRepository _cartRepository;
    private readonly IAddressRepository _addressRepository;
    private readonly IProductServiceClient _productClient;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOrderNotificationPublisher _notificationPublisher;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IOrderRepository orderRepository,
        ICartRepository cartRepository,
        IAddressRepository addressRepository,
        IProductServiceClient productClient,
        IUnitOfWork unitOfWork,
        IOrderNotificationPublisher notificationPublisher,
        ILogger<OrderService> logger)
    {
        _orderRepository = orderRepository;
        _cartRepository = cartRepository;
        _addressRepository = addressRepository;
        _productClient = productClient;
        _unitOfWork = unitOfWork;
        _notificationPublisher = notificationPublisher;
        _logger = logger;
    }

    /// <summary>
    /// Turns the current cart into a Pending order. Unit prices are read from the
    /// Product Service and the total is computed here, so the client never supplies a
    /// price. The order and the emptied cart are committed together with the stock
    /// reservation, and the cart is emptied as part of that same write.
    /// </summary>
    public async Task<OrderResponse> CreateOrderAsync(
        int userId,
        CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var order = await PrepareOrderFromCartAsync(userId, request.ShippingAddressId, cancellationToken);

        _orderRepository.Add(order);

        await SaveAndReserveStockAsync(order, cancellationToken);

        _logger.LogInformation(
            "Order {OrderNumber} ({OrderId}) created for user {UserId} with total {TotalAmount}",
            order.OrderNumber,
            order.Id,
            userId,
            order.TotalAmount);

        return await order.ToResponseAsync(_productClient, cancellationToken);
    }

    /// <summary>
    /// Writes the order and the emptied cart, then takes the stock the order needs.
    ///
    /// The order has to be written first: Product identifies a reservation by order id, and
    /// that id does not exist until the row is inserted. The two writes are in two different
    /// databases, so there is no single transaction spanning them. Instead this reserves
    /// after committing the order locally, and if any reservation is refused it rolls the
    /// order and the emptied cart back and gives back the stock that was already taken. What
    /// this does not provide is atomicity across both services, and it does not pretend to.
    /// </summary>
    public async Task SaveAndReserveStockAsync(Order order, CancellationToken cancellationToken)
    {
        var reserved = new List<OrderItem>();

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            foreach (var item in order.Items)
            {
                await _productClient.ReserveStockAsync(
                    new StockMutationRequest(order.Id, item.ProductId, item.Quantity),
                    cancellationToken);

                reserved.Add(item);
            }

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await ReleaseAsync(reserved, order.Id, cancellationToken);

            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Validates the cart and builds a Pending order from it, checking availability against
    /// the Product Service. Nothing is saved and no stock is taken, so the caller decides
    /// when to persist. Checkout relies on this to hold the order back until payment has
    /// been attempted. The reservation itself happens in <see cref="SaveAndReserveStockAsync"/>,
    /// once the order has an id to reserve against.
    /// </summary>
    public async Task<Order> PrepareOrderFromCartAsync(
        int userId,
        int shippingAddressId,
        CancellationToken cancellationToken)
    {
        var cart = await _cartRepository.GetByUserIdAsync(userId, cancellationToken);

        if (cart is null || cart.Items.Count == 0)
        {
            throw new ValidationException("Your cart is empty.");
        }

        var address = await GetOwnedAddressOrThrowAsync(userId, shippingAddressId, cancellationToken);

        var now = DateTime.UtcNow;

        var order = new Order
        {
            OrderNumber = await GenerateOrderNumberAsync(cancellationToken),
            UserId = userId,
            User = cart.User,
            ShippingAddressId = address.Id,
            ShippingAddress = address,
            Status = OrderStatus.Pending,
            TotalAmount = 0m,
            CreatedAt = now,
            UpdatedAt = now
        };

        foreach (var item in cart.Items)
        {
            var product = await _productClient.GetInventorySnapshotAsync(item.ProductId, cancellationToken);

            if (!product.IsActive)
            {
                throw new ValidationException($"Product '{product.Name}' is no longer available.");
            }

            if (item.Quantity > product.Quantity)
            {
                throw new ValidationException(
                    $"Only {product.Quantity} unit(s) of '{product.Name}' are in stock.");
            }

            var lineTotal = product.Price * item.Quantity;
            order.TotalAmount += lineTotal;

            order.Items.Add(new OrderItem
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = product.Price,
                TotalPrice = lineTotal
            });
        }

        cart.Items.Clear();
        cart.UpdatedAt = now;

        return order;
    }

    public async Task<PagedResult<OrderResponse>> GetOrdersAsync(
        int userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var result = await _orderRepository.GetPagedForUserAsync(userId, page, pageSize, cancellationToken);

        return await result.ToResponseAsync(_productClient, cancellationToken);
    }

    public async Task<PagedResult<OrderResponse>> GetAllOrdersAsync(OrderQuery query, CancellationToken cancellationToken)
    {
        var result = await _orderRepository.GetPagedAsync(query, cancellationToken);

        return await result.ToResponseAsync(_productClient, cancellationToken);
    }

    public async Task<OrderResponse> GetOrderByIdAsync(
        int userId,
        int orderId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken);

        // Reported as not found rather than forbidden, so a customer cannot learn that
        // another customer's order exists.
        if (order is null || (!isAdmin && order.UserId != userId))
        {
            throw new NotFoundException($"Order with id {orderId} was not found.");
        }

        return await order.ToResponseAsync(_productClient, cancellationToken);
    }

    public async Task<OrderResponse> UpdateStatusAsync(
        int orderId,
        UpdateOrderStatusRequest request,
        CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken)
            ?? throw new NotFoundException($"Order with id {orderId} was not found.");

        var allowed = AllowedTransitions.TryGetValue(order.Status, out var targets) ? targets : [];

        if (!allowed.Contains(request.Status))
        {
            throw new ValidationException(
                $"An order in status '{order.Status}' cannot be changed to '{request.Status}'. " +
                $"Allowed next statuses: {string.Join(", ", allowed)}.");
        }

        order.Status = request.Status;
        order.UpdatedAt = DateTime.UtcNow;

        var cancelled = request.Status == OrderStatus.Cancelled;

        if (cancelled)
        {
            CancelUnsettledPayments(order, order.UpdatedAt);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (cancelled)
        {
            // The cancellation is committed before the stock moves. If the release is then
            // interrupted, stock stays one order's worth too low, which is safe; the other
            // order would have left the order cancelled while its units were already for sale.
            await ReleaseAsync(order.Items, order.Id, cancellationToken);
        }

        _logger.LogInformation("Order {OrderNumber} moved to {Status}", order.OrderNumber, request.Status);

        await PublishStatusChangeAsync(order, cancellationToken);

        return await order.ToResponseAsync(_productClient, cancellationToken);
    }

    /// <summary>
    /// Cancels orders that have been waiting for payment since before the cutoff and
    /// returns their reserved stock. Driven by the abandoned-order cleanup job, which only
    /// runs when a timeout is configured.
    /// </summary>
    public async Task<int> CancelAbandonedOrdersAsync(DateTime createdBefore, CancellationToken cancellationToken)
    {
        var abandoned = await _orderRepository.GetPendingCreatedBeforeAsync(createdBefore, cancellationToken);

        if (abandoned.Count == 0)
        {
            return 0;
        }

        var now = DateTime.UtcNow;

        foreach (var order in abandoned)
        {
            // Same rules as an administrator cancelling the order by hand: cancel the order,
            // commit it, and only then give the units back.
            order.Status = OrderStatus.Cancelled;
            order.UpdatedAt = now;

            CancelUnsettledPayments(order, now);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Cleanup cancelled {Count} abandoned order(s)", abandoned.Count);

        foreach (var order in abandoned)
        {
            await ReleaseAsync(order.Items, order.Id, cancellationToken);
        }

        foreach (var order in abandoned)
        {
            await PublishStatusChangeAsync(order, cancellationToken);
        }

        return abandoned.Count;
    }

    private async Task PublishStatusChangeAsync(Order order, CancellationToken cancellationToken)
    {
        var notification = new OrderNotification(
            order.Id,
            order.OrderNumber,
            order.UserId,
            order.Status,
            Describe(order),
            DateTime.UtcNow);

        try
        {
            await _notificationPublisher.PublishAsync(notification, cancellationToken);
        }
        catch (Exception ex)
        {
            // The status change is already committed, so a queueing failure must not fail
            // the request that made it.
            _logger.LogWarning(
                ex,
                "Could not queue an order notification for order {OrderNumber}",
                order.OrderNumber);
        }
    }

    private static string Describe(Order order) => order.Status switch
    {
        OrderStatus.Confirmed => $"Order {order.OrderNumber} is confirmed.",
        OrderStatus.Processing => $"Order {order.OrderNumber} is being processed.",
        OrderStatus.Shipped => $"Order {order.OrderNumber} has shipped.",
        OrderStatus.Delivered => $"Order {order.OrderNumber} has been delivered.",
        OrderStatus.Cancelled => $"Order {order.OrderNumber} was cancelled.",
        _ => $"Order {order.OrderNumber} is {order.Status}."
    };

    /// <summary>
    /// Cancelling releases the units the order reserved. Only cancellable statuses can reach
    /// this point, and Product tracks each reservation against its order id, so stock is
    /// returned at most once per order.
    ///
    /// Deliberately best effort: the caller has already committed the cancellation, and a
    /// failed release must not turn a completed cancellation into a 500. It is logged as an
    /// error instead, and because Product's release is idempotent per order and product,
    /// replaying it later cannot credit the same units twice.
    /// </summary>
    private async Task ReleaseAsync(IEnumerable<OrderItem> items, int orderId, CancellationToken cancellationToken)
    {
        foreach (var item in items)
        {
            try
            {
                await _productClient.ReleaseStockAsync(
                    new StockMutationRequest(orderId, item.ProductId, item.Quantity),
                    cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Could not release reserved stock for order {OrderId} product {ProductId}",
                    orderId,
                    item.ProductId);
            }
        }
    }

    /// <summary>
    /// Moves a cancelled order's unsettled payment attempts to Cancelled, so a dead order
    /// does not keep a live-looking Pending or Failed payment attached to it.
    /// A payment that already succeeded is deliberately left alone: the money was taken and
    /// the mock gateway cannot refund it, so reporting it as Cancelled would hide a charge
    /// that really happened. Handling a refund is a payment-provider concern, not an
    /// order-status one.
    /// </summary>
    private static void CancelUnsettledPayments(Order order, DateTime now)
    {
        foreach (var payment in order.Payments)
        {
            if (payment.Status is PaymentStatus.Pending or PaymentStatus.Failed)
            {
                payment.Status = PaymentStatus.Cancelled;
                payment.UpdatedAt = now;
            }
        }
    }

    private async Task<Address> GetOwnedAddressOrThrowAsync(int userId, int addressId, CancellationToken cancellationToken)
    {
        var address = await _addressRepository.GetByIdAsync(addressId, cancellationToken);

        if (address is null || address.UserId != userId)
        {
            throw new NotFoundException($"Address with id {addressId} was not found.");
        }

        return address;
    }

    private async Task<string> GenerateOrderNumberAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= OrderNumberAttempts; attempt++)
        {
            var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
            var candidate = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{suffix}";

            if (!await _orderRepository.OrderNumberExistsAsync(candidate, cancellationToken))
            {
                return candidate;
            }
        }

        throw new ValidationException("Could not allocate a unique order number. Please try again.");
    }
}
