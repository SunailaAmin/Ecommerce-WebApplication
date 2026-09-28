using ECommerce.Application.Interfaces;
using ECommerce.Application.Notifications;
using ECommerce.Domain.Enums;
using ECommerce.Infrastructure.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace ECommerce.UnitTests.Notifications;

public class OrderNotificationWorkerTests
{
    private static OrderNotification Notification(string orderNumber) =>
        new(1, orderNumber, 5, OrderStatus.Confirmed, $"{orderNumber} confirmed", DateTime.UtcNow);

    /// <summary>
    /// Counts attempts and can be told to fail a fixed number of times, which is what
    /// makes the retry behaviour observable.
    /// </summary>
    private sealed class RecordingSender : IOrderNotificationSender
    {
        private readonly int _failuresToServe;
        private readonly TaskCompletionSource _handled =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public RecordingSender(int failuresToServe = 0)
        {
            _failuresToServe = failuresToServe;
        }

        public int Attempts { get; private set; }

        public List<string> Delivered { get; } = [];

        public Task Handled => _handled.Task;

        public async Task SendAsync(OrderNotification notification, CancellationToken cancellationToken)
        {
            Attempts++;

            if (Attempts <= _failuresToServe)
            {
                throw new InvalidOperationException("transport unavailable");
            }

            Delivered.Add(notification.OrderNumber);
            _handled.TrySetResult();
        }
    }

    private static (BackgroundService Worker, OrderNotificationQueue Queue, ServiceProvider Provider)
        CreateWorker(IOrderNotificationSender sender)
    {
        var queue = new OrderNotificationQueue(NullLogger<OrderNotificationQueue>.Instance);

        var services = new ServiceCollection();
        services.AddSingleton(sender);

        var provider = services.BuildServiceProvider();

        var worker = new OrderNotificationWorker(
            queue,
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<OrderNotificationWorker>.Instance);

        return (worker, queue, provider);
    }

    private static Task StartAsync(BackgroundService worker) => worker.StartAsync(CancellationToken.None);

    private static Task StopAsync(BackgroundService worker) => worker.StopAsync(CancellationToken.None);

    [Fact]
    public async Task DeliversAQueuedNotification()
    {
        var sender = new RecordingSender();
        var (worker, queue, provider) = CreateWorker(sender);

        await StartAsync(worker);
        await queue.PublishAsync(Notification("ORD-1"), CancellationToken.None);

        await sender.Handled.WaitAsync(TimeSpan.FromSeconds(10));

        await StopAsync(worker);
        provider.Dispose();

        Assert.Equal(["ORD-1"], sender.Delivered);
    }

    [Fact]
    public async Task RetriesUntilTheSenderSucceeds()
    {
        var sender = new RecordingSender(failuresToServe: 2);
        var (worker, queue, provider) = CreateWorker(sender);

        await StartAsync(worker);
        await queue.PublishAsync(Notification("ORD-2"), CancellationToken.None);

        await sender.Handled.WaitAsync(TimeSpan.FromSeconds(20));

        await StopAsync(worker);
        provider.Dispose();

        // Two failures then a success, so the event is not lost on a transient fault.
        Assert.Equal(3, sender.Attempts);
        Assert.Equal(["ORD-2"], sender.Delivered);
    }

    [Fact]
    public async Task GivesUpAfterTheLastAttemptWithoutBlockingTheQueue()
    {
        var sender = new RecordingSender(failuresToServe: int.MaxValue);
        var (worker, queue, provider) = CreateWorker(sender);

        await StartAsync(worker);
        await queue.PublishAsync(Notification("ORD-3"), CancellationToken.None);

        // The next event is what proves the worker moved past the poisoned one.
        await queue.PublishAsync(Notification("ORD-4"), CancellationToken.None);

        var deadline = DateTime.UtcNow.AddSeconds(30);

        while (sender.Attempts < 6 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100);
        }

        await StopAsync(worker);
        provider.Dispose();

        // The first event consumed all three attempts and was dropped; the second is then
        // retried, so the queue is still making progress.
        Assert.True(sender.Attempts >= 4, $"expected the worker to keep draining, saw {sender.Attempts} attempts");
    }
}
