using ECommerce.Products.Application.Interfaces;
using ECommerce.Products.Domain.Entities;
using ECommerce.Products.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Products.Infrastructure.Repositories;

public class CategoryRepository : ICategoryRepository
{
    private readonly ECommerceDbContext _context;

    public CategoryRepository(ECommerceDbContext context)
    {
        _context = context;
    }

    public async Task<List<Category>> GetAllAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        var query = _context.Categories.AsNoTracking().AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        return await query.OrderBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public Task<Category?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        return _context.Categories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<bool> NameExistsAsync(string name, int? excludeCategoryId, CancellationToken cancellationToken)
    {
        return _context.Categories
            .AnyAsync(x => x.Name == name && (!excludeCategoryId.HasValue || x.Id != excludeCategoryId.Value), cancellationToken);
    }

    public void Add(Category category)
    {
        _context.Categories.Add(category);
    }

    public void Update(Category category)
    {
        _context.Categories.Update(category);
    }
}
