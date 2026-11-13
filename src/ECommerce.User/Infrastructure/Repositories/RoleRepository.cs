using ECommerce.Users.Application.Interfaces;
using ECommerce.Users.Domain.Entities;
using ECommerce.Users.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Users.Infrastructure.Repositories;

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
