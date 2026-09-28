using ECommerce.Application.Interfaces;
using ECommerce.Domain.Entities;
using ECommerce.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Infrastructure.Repositories;

public class RoleRepository : IRoleRepository
{
    private readonly ECommerceDbContext _context;

    public RoleRepository(ECommerceDbContext context)
    {
        _context = context;
    }

    public Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken)
    {
        return _context.Roles
            .FirstOrDefaultAsync(x => x.Name == name, cancellationToken);
    }
}
