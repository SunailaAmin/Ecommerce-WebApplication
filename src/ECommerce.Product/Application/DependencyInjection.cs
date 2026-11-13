using ECommerce.Products.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Products.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ProductService>();
        services.AddScoped<CategoryService>();
        services.AddScoped<InventoryService>();

        return services;
    }
}
