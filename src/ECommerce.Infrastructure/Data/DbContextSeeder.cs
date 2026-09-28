using ECommerce.Domain;
using ECommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Infrastructure.Data;

public static class DbContextSeeder
{
    public static async Task SeedAsync(ECommerceDbContext context, CancellationToken cancellationToken = default)
    {
        var required = new[] { RoleNames.Admin, RoleNames.Customer };
        var existing = await context.Roles
            .Where(x => required.Contains(x.Name))
            .Select(x => x.Name)
            .ToListAsync(cancellationToken);

        var missing = required.Where(name => !existing.Contains(name)).ToList();

        if (missing.Count == 0)
        {
            return;
        }

        context.Roles.AddRange(missing.Select(name => new Role { Name = name }));
        await context.SaveChangesAsync(cancellationToken);
    }
}
