using ECommerce.Application.OrderProcessing;
using ECommerce.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ECommerce.Infrastructure.OrderProcessing;

/// <summary>
/// Periodically cancels orders that were never paid for, so the stock they are holding is
/// released. The job is inert unless a timeout is configured.
/// </summary>
public class AbandonedOrderCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OrderProcessingOptions _options;
    private readonly ILogger<AbandonedOrderCleanupService> _logger;

    public AbandonedOrderCleanupService(
        IServiceScopeFactory scopeFactory,
        IOptions<OrderProcessingOptions> options,
        ILogger<AbandonedOrderCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.AbandonedOrderTimeoutMinutes <= 0)
        {
            _logger.LogInformation(
                "Abandoned order cleanup is disabled; set {Section}:{Setting} to enable it",
                OrderProcessingOptions.SectionName,
                nameof(OrderProcessingOptions.AbandonedOrderTimeoutMinutes));

            return;
        }

        // Floored at a second so a mistyped setting cannot turn this into a busy loop.
        var interval = TimeSpan.FromMinutes(Math.Max(1d / 60d, _options.CleanupIntervalMinutes));

        _logger.LogInformation(
            "Abandoned order cleanup started, cancelling orders older than {Timeout} minute(s) every {Interval} minute(s)",
            _options.AbandonedOrderTimeoutMinutes,
            interval.TotalMinutes);

        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunOnceAsync(stoppingToken);
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-_options.AbandonedOrderTimeoutMinutes);

            using var scope = _scopeFactory.CreateScope();
            var orderService = scope.ServiceProvider.GetRequiredService<OrderService>();

            var cancelled = await orderService.CancelAbandonedOrdersAsync(cutoff, cancellationToken);

            if (cancelled > 0)
            {
                _logger.LogInformation("Cleanup cancelled {Count} order(s) awaiting payment", cancelled);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutting down.
        }
        catch (Exception ex)
        {
            // A failed pass must not kill the timer, otherwise cleanup would stop for the
            // lifetime of the process.
            _logger.LogError(ex, "Abandoned order cleanup pass failed");
        }
    }
}
