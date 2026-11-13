using ECommerce.Orders.Api.Hubs;
using ECommerce.Orders.Application.Interfaces;
using ECommerce.Orders.Application.Notifications;
using Microsoft.AspNetCore.SignalR;

namespace ECommerce.Orders.Api.Notifications;

/// <summary>
/// Delivers a queued order event to the customer's SignalR connections.
/// </summary>
public class SignalROrderNotificationSender : IOrderNotificationSender
{
    private readonly IHubContext<OrderNotificationHub> _hubContext;
    private readonly ILogger<SignalROrderNotificationSender> _logger;

    public SignalROrderNotificationSender(
        IHubContext<OrderNotificationHub> hubContext,
        ILogger<SignalROrderNotificationSender> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task SendAsync(OrderNotification notification, CancellationToken cancellationToken)
    {
        await _hubContext.Clients
            .Group(OrderNotificationHub.GroupFor(notification.UserId))
            .SendAsync(
                OrderNotificationHub.ClientMethod,
                notification,
                cancellationToken);

        _logger.LogDebug(
            "Sent {Status} notification for order {OrderNumber} to user {UserId}",
            notification.Status,
            notification.OrderNumber,
            notification.UserId);
    }
}
