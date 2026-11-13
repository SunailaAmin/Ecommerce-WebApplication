using ECommerce.Users.Application.Services;
using ECommerce.Users.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Users.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

        services.AddScoped<AuthService>();
        services.AddScoped<AddressService>();

        return services;
    }
}
