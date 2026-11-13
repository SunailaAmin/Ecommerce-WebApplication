using ECommerce.Payments.Application.Interfaces;
using ECommerce.Payments.Infrastructure.Data;
using ECommerce.Payments.Infrastructure.Payments;
using ECommerce.Payments.Infrastructure.Repositories;
using ECommerce.Shared.Authentication;
using ECommerce.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Payments.Infrastructure;

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
        services.AddScoped<IPaymentRepository, PaymentRepository>();

        // The gateway is the only piece that has to change when a real provider replaces the mock.
        services.AddOptions<PaymentOptions>()
            .Bind(configuration.GetSection(PaymentOptions.SectionName))
            .ValidateOnStart();
        services.AddScoped<IPaymentGateway, MockPaymentGateway>();

        services.AddJwtAuthentication(configuration);

        return services;
    }
}
