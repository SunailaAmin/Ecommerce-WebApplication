using System.Security.Claims;
using System.Text;
using ECommerce.Application.Caching;
using ECommerce.Application.Interfaces;
using ECommerce.Application.Notifications;
using ECommerce.Application.OrderProcessing;
using ECommerce.Domain.Entities;
using ECommerce.Infrastructure.Authentication;
using ECommerce.Infrastructure.Caching;
using ECommerce.Infrastructure.Data;
using ECommerce.Infrastructure.Notifications;
using ECommerce.Infrastructure.OrderProcessing;
using ECommerce.Infrastructure.Payments;
using ECommerce.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ECommerce.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        services.AddDbContext<ECommerceDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sql =>
            {
                // An order is loaded as one aggregate (user, address, items, products,
                // payments). Two collection navigations in a single query is exactly the
                // case EF warns about, so the split behaviour is stated rather than left
                // to chance.
                sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery);
            });

            // MARS is on, which disables savepoints. That is safe here because
            // CommitChangesInTransactionAsync rolls back explicitly on failure, which is
            // the handling EF asks for in place of a savepoint. Left alone, the warning
            // would repeat on every checkout and payment.
            options.ConfigureWarnings(w => w.Ignore(SqlServerEventId.SavepointsDisabledBecauseOfMARS));
        });

        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<ECommerceDbContext>());
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<ICartRepository, CartRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IAddressRepository, AddressRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<ITokenService, TokenService>();

        // The gateway is the only piece that has to change when a real provider replaces the mock.
        services.AddOptions<PaymentOptions>()
            .Bind(configuration.GetSection(PaymentOptions.SectionName))
            .ValidateOnStart();
        services.AddScoped<IPaymentGateway, MockPaymentGateway>();

        services.AddCatalogCaching(configuration);
        services.AddOrderNotifications(configuration);

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

    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

        if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < JwtOptions.MinimumKeyLength)
        {
            throw new InvalidOperationException(
                $"'{JwtOptions.SectionName}:Key' is not configured or is shorter than {JwtOptions.MinimumKeyLength} characters. " +
                $"Add it for local development with: dotnet user-secrets set \"{JwtOptions.SectionName}:Key\" \"<at least {JwtOptions.MinimumKeyLength} random characters>\"");
        }

        if (string.IsNullOrWhiteSpace(jwt.Issuer) || string.IsNullOrWhiteSpace(jwt.Audience))
        {
            throw new InvalidOperationException(
                $"'{JwtOptions.SectionName}:Issuer' and '{JwtOptions.SectionName}:Audience' must both be configured.");
        }

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName));

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false;
                options.SaveToken = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                    NameClaimType = ClaimTypes.Name,
                    RoleClaimType = ClaimTypes.Role
                };

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

        services.AddAuthorization();

        return services;
    }
}
