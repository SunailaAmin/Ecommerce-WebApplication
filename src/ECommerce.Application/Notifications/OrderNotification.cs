using ECommerce.Domain.Enums;

namespace ECommerce.Application.Notifications;

/// <summary>
/// A single order event, addressed to the customer who placed the order.
/// </summary>
public record OrderNotification(
    int OrderId,
    string OrderNumber,
    int UserId,
    OrderStatus Status,
    string Message,
    DateTime OccurredAt);
