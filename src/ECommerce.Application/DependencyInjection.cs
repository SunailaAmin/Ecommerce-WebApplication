using ECommerce.Application.Services;
using ECommerce.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

        services.AddScoped<ProductService>();
        services.AddScoped<CategoryService>();
        services.AddScoped<InventoryService>();
        services.AddScoped<AuthService>();
        services.AddScoped<CartService>();
        services.AddScoped<OrderService>();
        services.AddScoped<AddressService>();
        services.AddScoped<CheckoutService>();
        services.AddScoped<PaymentService>();

        return services;
    }
}
