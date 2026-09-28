using ECommerce.Application.DTOs.Categories;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Interfaces;
using ECommerce.Application.Services;
using ECommerce.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ECommerce.UnitTests.Services;

public class CategoryServiceTests
{
    private readonly Mock<ICategoryRepository> _categoryRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICatalogCache> _catalogCache = new();
    private readonly CategoryService _service;

    public CategoryServiceTests()
    {
        _service = new CategoryService(
            _categoryRepository.Object,
            _unitOfWork.Object,
            _catalogCache.Object,
            NullLogger<CategoryService>.Instance);

        // Read-through pass-through: the repository setups are what these tests assert on.
        _catalogCache
            .Setup(x => x.GetCategoriesAsync(
                It.IsAny<bool>(),
                It.IsAny<Func<CancellationToken, Task<List<CategoryResponse>>>>(),
                It.IsAny<CancellationToken>()))
            .Returns((bool _, Func<CancellationToken, Task<List<CategoryResponse>>> factory, CancellationToken ct)
                => factory(ct));
    }

    private static Category CreateCategory(int id = 1, string name = "Laptops", bool isActive = true) => new()
    {
        Id = id,
        Name = name,
        IsActive = isActive
    };

    [Fact]
    public async Task GetCategoriesAsync_ReturnsCategories()
    {
        _categoryRepository
            .Setup(x => x.GetAllAsync(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Category> { CreateCategory(), CreateCategory(2, "Phones") });

        var result = await _service.GetCategoriesAsync(false, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal("Laptops", result[0].Name);
    }

    [Fact]
    public async Task GetCategoryByIdAsync_ThrowsNotFound_WhenCategoryMissing()
    {
        _categoryRepository
            .Setup(x => x.GetByIdAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.GetCategoryByIdAsync(99, CancellationToken.None));
    }

    [Fact]
    public async Task CreateCategoryAsync_CreatesCategory()
    {
        _categoryRepository
            .Setup(x => x.NameExistsAsync("Laptops", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _categoryRepository
            .Setup(x => x.Add(It.IsAny<Category>()))
            .Callback<Category>(c => c.Id = 7);

        var result = await _service.CreateCategoryAsync(
            new CreateCategoryRequest { Name = "  Laptops  ", Description = "Portable" },
            CancellationToken.None);

        _categoryRepository.Verify(x => x.Add(It.Is<Category>(c =>
            c.Name == "Laptops" && c.Description == "Portable")), Times.Once);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(7, result.Id);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task CreateCategoryAsync_ThrowsConflict_WhenNameAlreadyUsed()
    {
        _categoryRepository
            .Setup(x => x.NameExistsAsync("Laptops", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await Assert.ThrowsAsync<ConflictException>(
            () => _service.CreateCategoryAsync(new CreateCategoryRequest { Name = "Laptops" }, CancellationToken.None));

        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateCategoryAsync_AppliesChanges()
    {
        var category = CreateCategory();
        _categoryRepository
            .Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);
        _categoryRepository
            .Setup(x => x.NameExistsAsync("Workstations", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.UpdateCategoryAsync(
            1,
            new UpdateCategoryRequest { Name = "Workstations", IsActive = false },
            CancellationToken.None);

        _categoryRepository.Verify(x => x.Update(category), Times.Once);
        Assert.Equal("Workstations", result.Name);
        Assert.False(result.IsActive);
    }

    [Fact]
    public async Task DeactivateCategoryAsync_SetsInactive()
    {
        var category = CreateCategory();
        _categoryRepository
            .Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);

        await _service.DeactivateCategoryAsync(1, CancellationToken.None);

        Assert.False(category.IsActive);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeactivateCategoryAsync_ThrowsNotFound_WhenCategoryMissing()
    {
        _categoryRepository
            .Setup(x => x.GetByIdAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.DeactivateCategoryAsync(99, CancellationToken.None));
    }
}
