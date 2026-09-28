using ECommerce.Application.DTOs;
using ECommerce.Application.DTOs.Products;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Interfaces;
using ECommerce.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ECommerce.Application.Services;

public class ProductService
{
    private readonly IProductRepository _productRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICatalogCache _catalogCache;
    private readonly ILogger<ProductService> _logger;

    public ProductService(
        IProductRepository productRepository,
        ICategoryRepository categoryRepository,
        IUnitOfWork unitOfWork,
        ICatalogCache catalogCache,
        ILogger<ProductService> logger)
    {
        _productRepository = productRepository;
        _categoryRepository = categoryRepository;
        _unitOfWork = unitOfWork;
        _catalogCache = catalogCache;
        _logger = logger;
    }

    public Task<PagedResult<ProductResponse>> GetProductsAsync(ProductQuery query, CancellationToken cancellationToken)
    {
        return _catalogCache.GetProductsAsync(
            BuildListCacheKey(query),
            ct => LoadProductsAsync(query, ct),
            cancellationToken);
    }

    private async Task<PagedResult<ProductResponse>> LoadProductsAsync(ProductQuery query, CancellationToken cancellationToken)
    {
        var result = await _productRepository.GetPagedAsync(query, cancellationToken);

        var items = result.Items.Select(ToResponse).ToList();

        return new PagedResult<ProductResponse>(items, result.TotalCount, result.Page, result.PageSize);
    }

    public Task<ProductResponse> GetProductByIdAsync(int id, CancellationToken cancellationToken)
    {
        return _catalogCache.GetProductAsync(
            id,
            async ct =>
            {
                var product = await _productRepository.GetByIdAsync(id, ct)
                    ?? throw new NotFoundException($"Product with id {id} was not found.");

                return ToResponse(product);
            },
            cancellationToken);
    }
    /// <summary>
    /// Every field the query can filter or order on has to be part of the key, otherwise
    /// one request's page would be served for another's search or sort.
    /// </summary>
    private static string BuildListCacheKey(ProductQuery query) => string.Join(
        '|',
        query.Page,
        query.PageSize,
        query.Search?.Trim().ToLowerInvariant() ?? string.Empty,
        query.CategoryId,
        query.IncludeInactive,
        query.SortBy?.Trim().ToLowerInvariant(),
        query.SortDescending);

    public async Task<ProductResponse> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken)
    {
        var sku = request.Sku.Trim();

        if (await _productRepository.SkuExistsAsync(sku, null, cancellationToken))
        {
            throw new ConflictException($"A product with SKU '{sku}' already exists.");
        }

        var category = await GetCategoryOrThrowAsync(request.CategoryId, cancellationToken);
        var now = DateTime.UtcNow;

        var product = new Product
        {
            Name = request.Name.Trim(),
            Description = request.Description,
            Price = request.Price,
            Sku = sku,
            ImageUrl = request.ImageUrl,
            CategoryId = category.Id,
            Category = category,
            IsActive = request.IsActive,
            CreatedAt = now,
            UpdatedAt = now,
            Inventory = new Inventory
            {
                Quantity = request.Quantity,
                UpdatedAt = now
            }
        };

        _productRepository.Add(product);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _catalogCache.InvalidateProductLists();

        _logger.LogInformation("Product {ProductId} with SKU {Sku} created", product.Id, product.Sku);

        return ToResponse(product);
    }

    public async Task<ProductResponse> UpdateProductAsync(int id, UpdateProductRequest request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Product with id {id} was not found.");

        var sku = request.Sku.Trim();

        if (await _productRepository.SkuExistsAsync(sku, id, cancellationToken))
        {
            throw new ConflictException($"A product with SKU '{sku}' already exists.");
        }

        var category = await GetCategoryOrThrowAsync(request.CategoryId, cancellationToken);

        product.Name = request.Name.Trim();
        product.Description = request.Description;
        product.Price = request.Price;
        product.Sku = sku;
        product.ImageUrl = request.ImageUrl;
        product.CategoryId = category.Id;
        product.Category = category;
        product.IsActive = request.IsActive;
        product.UpdatedAt = DateTime.UtcNow;

        _productRepository.Update(product);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _catalogCache.InvalidateProduct(product.Id);
        _catalogCache.InvalidateProductLists();

        _logger.LogInformation("Product {ProductId} updated", product.Id);

        return ToResponse(product);
    }

    public async Task DeactivateProductAsync(int id, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Product with id {id} was not found.");

        if (!product.IsActive)
        {
            return;
        }

        product.IsActive = false;
        product.UpdatedAt = DateTime.UtcNow;

        _productRepository.Update(product);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _catalogCache.InvalidateProduct(product.Id);
        _catalogCache.InvalidateProductLists();

        _logger.LogInformation("Product {ProductId} deactivated", product.Id);
    }

    private async Task<Category> GetCategoryOrThrowAsync(int categoryId, CancellationToken cancellationToken)
    {
        return await _categoryRepository.GetByIdAsync(categoryId, cancellationToken)
            ?? throw new ValidationException($"Category with id {categoryId} does not exist.");
    }

    private static ProductResponse ToResponse(Product product) => new(
        product.Id,
        product.Name,
        product.Description,
        product.Price,
        product.Sku,
        product.ImageUrl,
        product.CategoryId,
        product.Category.Name,
        product.IsActive,
        product.CreatedAt,
        product.UpdatedAt);
}
