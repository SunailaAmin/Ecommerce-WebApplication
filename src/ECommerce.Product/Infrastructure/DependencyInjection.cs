using ECommerce.Products.Application.Caching;
using ECommerce.Products.Application.Interfaces;
using ECommerce.Products.Infrastructure.Caching;
using ECommerce.Products.Infrastructure.Data;
using ECommerce.Products.Infrastructure.Repositories;
using ECommerce.Shared.Authentication;
using ECommerce.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Products.Infrastructure;

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
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<IStockReservationRepository, StockReservationRepository>();

        services.AddCatalogCaching(configuration);
        services.AddJwtAuthentication(configuration);

        return services;
    }

    /// <summary>
    /// Caches only the public catalog reads. Nothing user-specific is cached, so there is
    /// no cross-user leakage to guard against.
    /// </summary>
    public static IServiceCollection AddCatalogCaching(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMemoryCache();

        services.AddOptions<CatalogCacheOptions>()
            .Bind(configuration.GetSection(CatalogCacheOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<ICatalogCache, CatalogCache>();

        return services;
    }
}
