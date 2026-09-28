using ECommerce.Application.DTOs;
using ECommerce.Application.DTOs.Inventory;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Interfaces;
using ECommerce.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ECommerce.Application.Services;

public class InventoryService
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<InventoryService> _logger;

    public InventoryService(
        IInventoryRepository inventoryRepository,
        IUnitOfWork unitOfWork,
        ILogger<InventoryService> logger)
    {
        _inventoryRepository = inventoryRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<PagedResult<InventoryResponse>> GetInventoryAsync(InventoryQuery query, CancellationToken cancellationToken)
    {
        var result = await _inventoryRepository.GetPagedAsync(query, cancellationToken);

        var items = result.Items.Select(ToResponse).ToList();

        return new PagedResult<InventoryResponse>(items, result.TotalCount, result.Page, result.PageSize);
    }

    public async Task<InventoryResponse> GetInventoryByProductIdAsync(int productId, CancellationToken cancellationToken)
    {
        var inventory = await GetInventoryOrThrowAsync(productId, cancellationToken);

        return ToResponse(inventory);
    }

    public async Task<InventoryResponse> SetQuantityAsync(int productId, UpdateInventoryRequest request, CancellationToken cancellationToken)
    {
        var inventory = await GetInventoryOrThrowAsync(productId, cancellationToken);
        var previousQuantity = inventory.Quantity;

        inventory.Quantity = request.Quantity;
        inventory.UpdatedAt = DateTime.UtcNow;

        _inventoryRepository.Update(inventory);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Stock for product {ProductId} set from {PreviousQuantity} to {Quantity}",
            productId,
            previousQuantity,
            inventory.Quantity);

        return ToResponse(inventory);
    }

    public async Task<InventoryResponse> AdjustQuantityAsync(int productId, AdjustInventoryRequest request, CancellationToken cancellationToken)
    {
        if (request.Delta == 0)
        {
            throw new ValidationException("Delta must not be zero.");
        }

        var inventory = await GetInventoryOrThrowAsync(productId, cancellationToken);
        var newQuantity = inventory.Quantity + request.Delta;

        if (newQuantity < 0)
        {
            throw new ValidationException(
                $"Cannot remove {Math.Abs(request.Delta)} units, only {inventory.Quantity} in stock.");
        }

        inventory.Quantity = newQuantity;
        inventory.UpdatedAt = DateTime.UtcNow;

        _inventoryRepository.Update(inventory);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Stock for product {ProductId} adjusted by {Delta} to {Quantity}",
            productId,
            request.Delta,
            newQuantity);

        return ToResponse(inventory);
    }

    private async Task<Inventory> GetInventoryOrThrowAsync(int productId, CancellationToken cancellationToken)
    {
        return await _inventoryRepository.GetByProductIdAsync(productId, cancellationToken)
            ?? throw new NotFoundException($"Inventory for product {productId} was not found.");
    }

    private static InventoryResponse ToResponse(Inventory inventory) => new(
        inventory.ProductId,
        inventory.Product.Name,
        inventory.Product.Sku,
        inventory.Quantity,
        inventory.Quantity > 0,
        inventory.UpdatedAt);
}
