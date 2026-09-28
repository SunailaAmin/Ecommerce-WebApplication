using ECommerce.Application.Notifications;

namespace ECommerce.Application.Interfaces;

/// <summary>
/// The transport that actually delivers a queued order event. Implemented in the API
/// project by SignalR, which keeps the background worker free of any hub dependency.
/// </summary>
public interface IOrderNotificationSender
{
    Task SendAsync(OrderNotification notification, CancellationToken cancellationToken);
}
