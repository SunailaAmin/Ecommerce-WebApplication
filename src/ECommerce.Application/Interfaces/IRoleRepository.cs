using ECommerce.Domain.Entities;

namespace ECommerce.Application.Interfaces;

public interface IRoleRepository
{
    Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken);
}
