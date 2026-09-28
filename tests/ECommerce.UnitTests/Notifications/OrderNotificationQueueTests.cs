using ECommerce.Application.Notifications;
using ECommerce.Domain.Enums;
using ECommerce.Infrastructure.Notifications;
using Microsoft.Extensions.Logging.Abstractions;

namespace ECommerce.UnitTests.Notifications;

public class OrderNotificationQueueTests
{
    private static OrderNotification Notification(string orderNumber) =>
        new(1, orderNumber, 5, OrderStatus.Confirmed, $"{orderNumber} confirmed", DateTime.UtcNow);

    private static OrderNotificationQueue CreateQueue() =>
        new(NullLogger<OrderNotificationQueue>.Instance);

    [Fact]
    public async Task PublishAsync_MakesTheNotificationReadable()
    {
        var queue = CreateQueue();

        await queue.PublishAsync(Notification("ORD-1"), CancellationToken.None);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        string? received = null;

        await foreach (var notification in queue.ReadAllAsync(cts.Token))
        {
            received = notification.OrderNumber;
            break;
        }

        Assert.Equal("ORD-1", received);
    }

    [Fact]
    public async Task PublishAsync_KeepsNotificationsInOrder()
    {
        var queue = CreateQueue();

        await queue.PublishAsync(Notification("ORD-1"), CancellationToken.None);
        await queue.PublishAsync(Notification("ORD-2"), CancellationToken.None);
        await queue.PublishAsync(Notification("ORD-3"), CancellationToken.None);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var received = new List<string>();

        await foreach (var notification in queue.ReadAllAsync(cts.Token))
        {
            received.Add(notification.OrderNumber);

            if (received.Count == 3)
            {
                break;
            }
        }

        Assert.Equal(["ORD-1", "ORD-2", "ORD-3"], received);
    }
}
