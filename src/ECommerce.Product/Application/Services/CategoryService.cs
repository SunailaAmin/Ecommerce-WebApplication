using ECommerce.Products.Application.DTOs.Categories;
using ECommerce.Shared.Exceptions;
using ECommerce.Products.Application.Interfaces;
using ECommerce.Products.Domain.Entities;
using Microsoft.Extensions.Logging;
using ECommerce.Shared.Data;

namespace ECommerce.Products.Application.Services;

public class CategoryService
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICatalogCache _catalogCache;
    private readonly ILogger<CategoryService> _logger;

    public CategoryService(
        ICategoryRepository categoryRepository,
        IUnitOfWork unitOfWork,
        ICatalogCache catalogCache,
        ILogger<CategoryService> logger)
    {
        _categoryRepository = categoryRepository;
        _unitOfWork = unitOfWork;
        _catalogCache = catalogCache;
        _logger = logger;
    }

    public Task<List<CategoryResponse>> GetCategoriesAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        return _catalogCache.GetCategoriesAsync(
            includeInactive,
            async ct =>
            {
                var categories = await _categoryRepository.GetAllAsync(includeInactive, ct);

                return categories.Select(ToResponse).ToList();
            },
            cancellationToken);
    }

    public async Task<CategoryResponse> GetCategoryByIdAsync(int id, CancellationToken cancellationToken)
    {
        var category = await _categoryRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Category with id {id} was not found.");

        return ToResponse(category);
    }

    public async Task<CategoryResponse> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();

        if (await _categoryRepository.NameExistsAsync(name, null, cancellationToken))
        {
            throw new ConflictException($"A category named '{name}' already exists.");
        }

        var now = DateTime.UtcNow;

        var category = new Category
        {
            Name = name,
            Description = request.Description,
            CreatedAt = now,
            UpdatedAt = now
        };

        _categoryRepository.Add(category);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _catalogCache.InvalidateCategories();

        _logger.LogInformation("Category {CategoryId} ({Name}) created", category.Id, category.Name);

        return ToResponse(category);
    }

    public async Task<CategoryResponse> UpdateCategoryAsync(int id, UpdateCategoryRequest request, CancellationToken cancellationToken)
    {
        var category = await _categoryRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Category with id {id} was not found.");

        var name = request.Name.Trim();

        if (await _categoryRepository.NameExistsAsync(name, id, cancellationToken))
        {
            throw new ConflictException($"A category named '{name}' already exists.");
        }

        category.Name = name;
        category.Description = request.Description;
        category.IsActive = request.IsActive;
        category.UpdatedAt = DateTime.UtcNow;

        _categoryRepository.Update(category);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _catalogCache.InvalidateCategories();

        _logger.LogInformation("Category {CategoryId} updated", category.Id);

        return ToResponse(category);
    }

    public async Task DeactivateCategoryAsync(int id, CancellationToken cancellationToken)
    {
        var category = await _categoryRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Category with id {id} was not found.");

        if (!category.IsActive)
        {
            return;
        }

        category.IsActive = false;
        category.UpdatedAt = DateTime.UtcNow;

        _categoryRepository.Update(category);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _catalogCache.InvalidateCategories();

        _logger.LogInformation("Category {CategoryId} deactivated", category.Id);
    }

    private static CategoryResponse ToResponse(Category category) => new(
        category.Id,
        category.Name,
        category.Description,
        category.IsActive,
        category.CreatedAt,
        category.UpdatedAt);
}
