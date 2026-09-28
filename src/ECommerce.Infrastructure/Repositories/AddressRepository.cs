using ECommerce.Application.Interfaces;
using ECommerce.Domain.Entities;
using ECommerce.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Infrastructure.Repositories;

public class AddressRepository : IAddressRepository
{
    private readonly ECommerceDbContext _context;

    public AddressRepository(ECommerceDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Address>> GetByUserIdAsync(int userId, CancellationToken cancellationToken)
    {
        return await _context.Addresses
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<Address?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        return _context.Addresses.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<bool> IsUsedByAnyOrderAsync(int id, CancellationToken cancellationToken)
    {
        return _context.Orders.AnyAsync(x => x.ShippingAddressId == id, cancellationToken);
    }

    public void Add(Address address)
    {
        _context.Addresses.Add(address);
    }

    public void Delete(Address address)
    {
        _context.Addresses.Remove(address);
    }
}
