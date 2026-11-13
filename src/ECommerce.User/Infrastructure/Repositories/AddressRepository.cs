using ECommerce.Users.Application.Interfaces;
using ECommerce.Users.Domain.Entities;
using ECommerce.Users.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ECommerce.Users.Infrastructure.Repositories;

public class AddressRepository : IAddressRepository
{
    private readonly ECommerceDbContext _context;
    private readonly ILogger<AddressRepository> _logger;

    public AddressRepository(ECommerceDbContext context, ILogger<AddressRepository> logger)
    {
        _context = context;
        _logger = logger;
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
        // Orders live in the Order service now, so this database cannot answer the question.
        // Until the User -> Order HTTP check arrives in Phase F the guard is not enforced,
        // which fails open: deletion succeeds for an address an order still references.
        // Logged on every call so the gap cannot pass unnoticed.
        _logger.LogWarning(
            "Address {AddressId} was checked for order usage without the Order service; the in-use guard is not enforced",
            id);

        return Task.FromResult(false);
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
