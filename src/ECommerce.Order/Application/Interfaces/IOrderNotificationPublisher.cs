using ECommerce.Orders.Application.Notifications;

namespace ECommerce.Orders.Application.Interfaces;

/// <summary>
/// Accepts order events for background delivery. Publishing never blocks on the
/// notification itself, so a slow or unavailable client cannot delay an API response.
/// </summary>
public interface IOrderNotificationPublisher
{
    Task PublishAsync(OrderNotification notification, CancellationToken cancellationToken);
}
