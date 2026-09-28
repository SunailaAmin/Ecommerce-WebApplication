using ECommerce.Application.Interfaces;
using ECommerce.Domain.Entities;
using ECommerce.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly ECommerceDbContext _context;

    public UserRepository(ECommerceDbContext context)
    {
        _context = context;
    }

    public Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        return _context.Users
            .Include(x => x.Role)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken)
    {
        return _context.Users
            .Include(x => x.Role)
            .FirstOrDefaultAsync(x => x.Email == email, cancellationToken);
    }

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken)
    {
        return _context.Users.AnyAsync(x => x.Email == email, cancellationToken);
    }

    public void Add(User user)
    {
        _context.Users.Add(user);
    }

    public void Update(User user)
    {
        _context.Users.Update(user);
    }
}
