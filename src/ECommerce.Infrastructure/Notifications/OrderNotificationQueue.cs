using System.Threading.Channels;
using ECommerce.Application.Interfaces;
using ECommerce.Application.Notifications;
using Microsoft.Extensions.Logging;

namespace ECommerce.Infrastructure.Notifications;

/// <summary>
/// In-process queue for order notifications, backed by a bounded channel.
/// </summary>
/// <remarks>
/// A channel is enough here: notifications are short-lived and losing one when the process
/// stops is preferable to the operational cost of an external job store. The bound keeps a
/// flood of events from growing the queue without limit, and the oldest pending event is
/// dropped when it is reached.
/// </remarks>
public class OrderNotificationQueue : IOrderNotificationPublisher
{
    private const int Capacity = 256;

    private readonly Channel<OrderNotification> _channel =
        Channel.CreateBounded<OrderNotification>(new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true
        });

    private readonly ILogger<OrderNotificationQueue> _logger;

    public OrderNotificationQueue(ILogger<OrderNotificationQueue> logger)
    {
        _logger = logger;
    }

    public async Task PublishAsync(OrderNotification notification, CancellationToken cancellationToken)
    {
        if (_channel.Writer.TryWrite(notification))
        {
            return;
        }

        // Only reachable if the host is shutting down and the worker has already stopped.
        _logger.LogWarning(
            "Order notification for order {OrderNumber} was dropped because the queue is shutting down",
            notification.OrderNumber);
    }

    public IAsyncEnumerable<OrderNotification> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
