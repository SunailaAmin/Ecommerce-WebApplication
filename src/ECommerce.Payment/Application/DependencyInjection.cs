using ECommerce.Payments.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Payments.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<PaymentService>();

        return services;
    }
}
