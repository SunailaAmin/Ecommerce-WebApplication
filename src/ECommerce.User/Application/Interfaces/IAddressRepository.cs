using ECommerce.Users.Domain.Entities;

namespace ECommerce.Users.Application.Interfaces;

public interface IAddressRepository
{
    Task<IReadOnlyList<Address>> GetByUserIdAsync(int userId, CancellationToken cancellationToken);

    Task<Address?> GetByIdAsync(int id, CancellationToken cancellationToken);

    /// <summary>
    /// True when an order still references this address, which makes it undeletable.
    /// </summary>
    Task<bool> IsUsedByAnyOrderAsync(int id, CancellationToken cancellationToken);

    void Add(Address address);

    void Delete(Address address);
}
