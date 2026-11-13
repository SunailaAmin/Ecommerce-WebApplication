using ECommerce.Users.Domain.Entities;

namespace ECommerce.Users.Application.Interfaces;

public interface IRoleRepository
{
    Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken);
}
