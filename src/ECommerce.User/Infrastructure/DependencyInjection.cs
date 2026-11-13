using ECommerce.Users.Application.Interfaces;
using ECommerce.Users.Infrastructure.Authentication;
using ECommerce.Users.Infrastructure.Data;
using ECommerce.Users.Infrastructure.Repositories;
using ECommerce.Shared.Authentication;
using ECommerce.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Users.Infrastructure;

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
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IAddressRepository, AddressRepository>();
        services.AddScoped<ITokenService, TokenService>();

        services.AddJwtAuthentication(configuration);

        return services;
    }
}
