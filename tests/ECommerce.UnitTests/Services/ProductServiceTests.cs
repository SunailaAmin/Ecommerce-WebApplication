using ECommerce.Application.DTOs;
using ECommerce.Application.DTOs.Products;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Interfaces;
using ECommerce.Application.Services;
using ECommerce.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ECommerce.UnitTests.Services;

public class ProductServiceTests
{
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<ICategoryRepository> _categoryRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICatalogCache> _catalogCache = new();
    private readonly ProductService _service;

    public ProductServiceTests()
    {
        _service = new ProductService(
            _productRepository.Object,
            _categoryRepository.Object,
            _unitOfWork.Object,
            _catalogCache.Object,
            NullLogger<ProductService>.Instance);

        // The cache is a read-through pass-through in these tests: the repository setups
        // below are what the tests are actually asserting on. Cache behaviour itself is
        // covered by CatalogCacheTests.
        _catalogCache
            .Setup(x => x.GetProductsAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<PagedResult<ProductResponse>>>>(),
                It.IsAny<CancellationToken>()))
            .Returns((string _, Func<CancellationToken, Task<PagedResult<ProductResponse>>> factory, CancellationToken ct)
                => factory(ct));

        _catalogCache
            .Setup(x => x.GetProductAsync(
                It.IsAny<int>(),
                It.IsAny<Func<CancellationToken, Task<ProductResponse>>>(),
                It.IsAny<CancellationToken>()))
            .Returns((int _, Func<CancellationToken, Task<ProductResponse>> factory, CancellationToken ct)
                => factory(ct));
    }

    private static Category CreateCategory(int id = 1) => new()
    {
        Id = id,
        Name = "Laptops",
        IsActive = true
    };

    private static Product CreateProduct(int id = 10, string sku = "LAP-1") => new()
    {
        Id = id,
        Name = "Pro Laptop",
        Description = "A laptop",
        Price = 1500m,
        Sku = sku,
        CategoryId = 1,
        Category = CreateCategory(),
        IsActive = true
    };

    private void ArrangeCategoryLookup()
    {
        _categoryRepository
            .Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCategory());
    }

    [Fact]
    public async Task GetProductsAsync_ReturnsPagedProducts()
    {
        var query = new ProductQuery { Page = 1, PageSize = 10 };
        var products = new List<Product> { CreateProduct() };

        _productRepository
            .Setup(x => x.GetPagedAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Application.DTOs.PagedResult<Product>(products, 1, 1, 10));

        var result = await _service.GetProductsAsync(query, CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(1, result.TotalPages);
        Assert.Equal("Laptops", result.Items[0].CategoryName);
    }

    [Fact]
    public async Task GetProductByIdAsync_ReturnsProduct()
    {
        _productRepository
            .Setup(x => x.GetByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateProduct());

        var result = await _service.GetProductByIdAsync(10, CancellationToken.None);

        Assert.Equal(10, result.Id);
        Assert.Equal(1500m, result.Price);
    }

    [Fact]
    public async Task GetProductByIdAsync_ThrowsNotFound_WhenProductMissing()
    {
        _productRepository
            .Setup(x => x.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.GetProductByIdAsync(999, CancellationToken.None));
    }

    [Fact]
    public async Task CreateProductAsync_CreatesProductWithInventory()
    {
        ArrangeCategoryLookup();
        _productRepository
            .Setup(x => x.SkuExistsAsync("LAP-1", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _productRepository
            .Setup(x => x.Add(It.IsAny<Product>()))
            .Callback<Product>(p => p.Id = 42);

        var request = new CreateProductRequest
        {
            Name = "  Pro Laptop  ",
            Price = 1500m,
            Sku = "  LAP-1  ",
            CategoryId = 1,
            Quantity = 25
        };

        var result = await _service.CreateProductAsync(request, CancellationToken.None);

        _productRepository.Verify(x => x.Add(It.Is<Product>(p =>
            p.Name == "Pro Laptop" &&
            p.Sku == "LAP-1" &&
            p.Inventory!.Quantity == 25)), Times.Once);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(42, result.Id);
    }

    [Fact]
    public async Task CreateProductAsync_ThrowsConflict_WhenSkuAlreadyUsed()
    {
        _productRepository
            .Setup(x => x.SkuExistsAsync("LAP-1", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = new CreateProductRequest { Name = "Pro Laptop", Price = 1500m, Sku = "LAP-1", CategoryId = 1 };

        await Assert.ThrowsAsync<ConflictException>(
            () => _service.CreateProductAsync(request, CancellationToken.None));

        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateProductAsync_ThrowsValidation_WhenCategoryMissing()
    {
        _productRepository
            .Setup(x => x.SkuExistsAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _categoryRepository
            .Setup(x => x.GetByIdAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category?)null);

        var request = new CreateProductRequest { Name = "Ghost", Price = 10m, Sku = "GH-1", CategoryId = 99 };

        await Assert.ThrowsAsync<ValidationException>(
            () => _service.CreateProductAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateProductAsync_AppliesChanges()
    {
        var product = CreateProduct();
        _productRepository
            .Setup(x => x.GetByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        _productRepository
            .Setup(x => x.SkuExistsAsync("LAP-2", 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        ArrangeCategoryLookup();

        var request = new UpdateProductRequest
        {
            Name = "Pro Laptop 2",
            Price = 1700m,
            Sku = "LAP-2",
            CategoryId = 1,
            IsActive = true
        };

        var result = await _service.UpdateProductAsync(10, request, CancellationToken.None);

        _productRepository.Verify(x => x.Update(product), Times.Once);
        Assert.Equal("Pro Laptop 2", result.Name);
        Assert.Equal(1700m, result.Price);
        Assert.Equal("LAP-2", result.Sku);
    }

    [Fact]
    public async Task UpdateProductAsync_ThrowsNotFound_WhenProductMissing()
    {
        _productRepository
            .Setup(x => x.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        var request = new UpdateProductRequest { Name = "X", Price = 1m, Sku = "X-1", CategoryId = 1 };

        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.UpdateProductAsync(999, request, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateProductAsync_ThrowsConflict_WhenSkuBelongsToAnotherProduct()
    {
        _productRepository
            .Setup(x => x.GetByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateProduct());
        _productRepository
            .Setup(x => x.SkuExistsAsync("LAP-9", 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = new UpdateProductRequest { Name = "Pro Laptop", Price = 1500m, Sku = "LAP-9", CategoryId = 1 };

        await Assert.ThrowsAsync<ConflictException>(
            () => _service.UpdateProductAsync(10, request, CancellationToken.None));
    }

    [Fact]
    public async Task DeactivateProductAsync_SetsInactive()
    {
        var product = CreateProduct();
        _productRepository
            .Setup(x => x.GetByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        await _service.DeactivateProductAsync(10, CancellationToken.None);

        Assert.False(product.IsActive);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeactivateProductAsync_SkipsSave_WhenAlreadyInactive()
    {
        var product = CreateProduct();
        product.IsActive = false;
        _productRepository
            .Setup(x => x.GetByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        await _service.DeactivateProductAsync(10, CancellationToken.None);

        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
