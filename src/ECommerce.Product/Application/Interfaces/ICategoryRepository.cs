using ECommerce.Products.Domain.Entities;

namespace ECommerce.Products.Application.Interfaces;

public interface ICategoryRepository
{
    Task<List<Category>> GetAllAsync(bool includeInactive, CancellationToken cancellationToken);

    Task<Category?> GetByIdAsync(int id, CancellationToken cancellationToken);

    Task<bool> NameExistsAsync(string name, int? excludeCategoryId, CancellationToken cancellationToken);

    void Add(Category category);

    void Update(Category category);
}
