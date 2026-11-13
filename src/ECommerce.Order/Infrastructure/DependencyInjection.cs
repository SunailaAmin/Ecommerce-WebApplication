using ECommerce.Orders.Application.Interfaces;
using ECommerce.Orders.Application.Notifications;
using ECommerce.Orders.Application.Options;
using ECommerce.Orders.Application.OrderProcessing;
using ECommerce.Orders.Infrastructure.Clients;
using ECommerce.Orders.Infrastructure.Data;
using ECommerce.Orders.Infrastructure.Notifications;
using ECommerce.Orders.Infrastructure.OrderProcessing;
using ECommerce.Orders.Infrastructure.Repositories;
using ECommerce.Shared.Authentication;
using ECommerce.Shared.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ECommerce.Orders.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        services.AddDbContext<ECommerceDbContext>(options =>
        {
            options.UseSqlServer(connectionString);

            // MARS is on, which disables savepoints. That is safe here because
            // CommitChangesInTransactionAsync rolls back explicitly on failure, which is the
            // handling EF asks for in place of a savepoint.
            options.ConfigureWarnings(w => w.Ignore(SqlServerEventId.SavepointsDisabledBecauseOfMARS));
        });

        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<ECommerceDbContext>());
        services.AddScoped<ICartRepository, CartRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();

        services.AddProductServiceClient(configuration);

        services.AddOrderNotifications(configuration);

        // Same token validation as every other service, plus reading the token from the
        // query string for the notification hub, which is how the browser's WebSocket
        // connection authenticates.
        services.AddJwtAuthentication(configuration, options =>
        {
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];

                    if (!string.IsNullOrEmpty(accessToken) &&
                        context.Request.Path.StartsWithSegments(OrderNotificationRoutes.Hub))
                    {
                        context.Token = accessToken;
                    }

                    return Task.CompletedTask;
                }
            };
        });

        return services;
    }

    /// <summary>
    /// Order reaches Product through IHttpClientFactory, so the connection pool, the base
    /// address and the timeout are all managed centrally rather than per call. No
    /// HttpClient is ever constructed directly, and the address and service key come from
    /// configuration so a deployment repoints the client without a rebuild.
    /// </summary>
    public static IServiceCollection AddProductServiceClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ProductServiceOptions>()
            .Bind(configuration.GetSection(ProductServiceOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl), "Services:Product:BaseUrl must be configured.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.ServiceKey), "Services:Product:ServiceKey must be configured.")
            .ValidateOnStart();

        services.AddHttpClient<IProductServiceClient, ProductServiceClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<ProductServiceOptions>>().Value;

            client.BaseAddress = new Uri(options.BaseUrl);

            // Deliberately short. Checkout waits on this call, so a slow Product Service should
            // fail the request quickly instead of holding a customer's checkout open.
            // Retries and circuit breaking are deliberately out of scope for this phase.
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        return services;
    }

    /// <summary>
    /// Order notifications are queued in memory and delivered by a hosted worker, so an API
    /// request is never delayed by the notification path.
    /// </summary>
    public static IServiceCollection AddOrderNotifications(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<OrderProcessingOptions>()
            .Bind(configuration.GetSection(OrderProcessingOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<OrderNotificationQueue>();
        services.AddSingleton<IOrderNotificationPublisher>(provider => provider.GetRequiredService<OrderNotificationQueue>());

        services.AddHostedService<OrderNotificationWorker>();
        services.AddHostedService<AbandonedOrderCleanupService>();

        return services;
    }
}
