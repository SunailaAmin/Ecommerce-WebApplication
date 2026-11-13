using ECommerce.Orders.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Orders.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CartService>();
        services.AddScoped<OrderService>();
        services.AddScoped<CheckoutService>();

        return services;
    }
}
