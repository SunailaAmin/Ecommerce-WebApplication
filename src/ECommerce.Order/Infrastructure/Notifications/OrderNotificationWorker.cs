using ECommerce.Orders.Application.Interfaces;
using ECommerce.Orders.Application.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ECommerce.Orders.Infrastructure.Notifications;

/// <summary>
/// Drains the notification queue and hands each event to the registered sender.
/// </summary>
/// <remarks>
/// A sender failure is retried a few times with a short backoff rather than surfaced to the
/// caller, because the API request that produced the event has already been answered. A
/// notification that is still failing after the last attempt is logged and dropped so one
/// bad event cannot stall the queue.
/// </remarks>
public class OrderNotificationWorker : BackgroundService
{
    private const int MaxAttempts = 3;

    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    private readonly OrderNotificationQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrderNotificationWorker> _logger;

    public OrderNotificationWorker(
        OrderNotificationQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<OrderNotificationWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Order notification worker started");

        try
        {
            await foreach (var notification in _queue.ReadAllAsync(stoppingToken))
            {
                await DeliverAsync(notification, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
        catch (Exception ex)
        {
            // The worker is the last line of defence for notifications, so an unexpected
            // failure is logged loudly rather than taking the host down.
            _logger.LogError(ex, "Order notification worker stopped unexpectedly");
        }
    }

    private async Task DeliverAsync(OrderNotification notification, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                // Resolved per message so the sender is free to be registered as scoped.
                using var scope = _scopeFactory.CreateScope();
                var sender = scope.ServiceProvider.GetRequiredService<IOrderNotificationSender>();

                await sender.SendAsync(notification, cancellationToken);

                _logger.LogInformation(
                    "Order notification delivered for order {OrderNumber} with status {Status}",
                    notification.OrderNumber,
                    notification.Status);

                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Attempt {Attempt} of {MaxAttempts} to deliver the notification for order {OrderNumber} failed",
                    attempt,
                    MaxAttempts,
                    notification.OrderNumber);

                if (attempt < MaxAttempts)
                {
                    await Task.Delay(RetryDelay, cancellationToken);
                }
            }
        }

        _logger.LogError(
            "Dropping the notification for order {OrderNumber} after {MaxAttempts} failed attempts",
            notification.OrderNumber,
            MaxAttempts);
    }
}
