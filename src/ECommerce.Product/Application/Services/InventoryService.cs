using ECommerce.Products.Application.DTOs;
using ECommerce.Products.Application.DTOs.Inventory;
using ECommerce.Shared.Exceptions;
using ECommerce.Products.Application.Interfaces;
using ECommerce.Products.Domain.Entities;
using Microsoft.Extensions.Logging;
using ECommerce.Shared.Data;
using ECommerce.Shared.DTOs;

namespace ECommerce.Products.Application.Services;

public class InventoryService
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IStockReservationRepository _reservationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<InventoryService> _logger;

    public InventoryService(
        IInventoryRepository inventoryRepository,
        IStockReservationRepository reservationRepository,
        IUnitOfWork unitOfWork,
        ILogger<InventoryService> logger)
    {
        _inventoryRepository = inventoryRepository;
        _reservationRepository = reservationRepository;
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

    /// <summary>
    /// One read of everything the Order Service asks for, so a cart or an order line costs
    /// a single call rather than a product read plus an inventory read.
    /// </summary>
    public async Task<InventorySnapshotResponse> GetSnapshotAsync(int productId, CancellationToken cancellationToken)
    {
        var inventory = await GetInventoryOrThrowAsync(productId, cancellationToken);

        return ToSnapshot(inventory);
    }

    /// <summary>
    /// Removes units from stock on behalf of an order. The decrement and the ledger row are
    /// written in one local transaction, so stock can never move without a record of who
    /// took it. The ledger makes the call idempotent: replaying it for an order that has
    /// already reserved reports Applied = false and does not decrement again.
    /// </summary>
    public async Task<StockReservationResponse> ReserveAsync(StockMutationRequest request, CancellationToken cancellationToken)
    {
        if (request.Quantity <= 0)
        {
            throw new ValidationException("Quantity must be greater than zero.");
        }

        var inventory = await GetInventoryOrThrowAsync(request.ProductId, cancellationToken);

        if (!inventory.Product.IsActive)
        {
            throw new ConflictException($"Product '{inventory.Product.Name}' is no longer available.");
        }

        var existing = await _reservationRepository.FindAsync(request.OrderId, request.ProductId, cancellationToken);

        if (existing is not null)
        {
            _logger.LogInformation(
                "Idempotent replay of reserve for order {OrderId} product {ProductId}; stock untouched",
                request.OrderId,
                request.ProductId);

            return new StockReservationResponse(
                request.OrderId,
                request.ProductId,
                existing.Quantity,
                Applied: false,
                inventory.Quantity);
        }

        if (inventory.Quantity < request.Quantity)
        {
            throw new ConflictException(
                $"Only {inventory.Quantity} unit(s) of '{inventory.Product.Name}' are in stock.");
        }

        var remaining = inventory.Quantity - request.Quantity;

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            inventory.Quantity = remaining;
            inventory.UpdatedAt = DateTime.UtcNow;
            _inventoryRepository.Update(inventory);

            _reservationRepository.Add(new InventoryReservation
            {
                OrderId = request.OrderId,
                ProductId = request.ProductId,
                Quantity = request.Quantity,
                CreatedAt = DateTime.UtcNow
            });

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        _logger.LogInformation(
            "Reserved {Quantity} unit(s) of product {ProductId} for order {OrderId}; {Remaining} left",
            request.Quantity,
            request.ProductId,
            request.OrderId,
            remaining);

        return new StockReservationResponse(
            request.OrderId,
            request.ProductId,
            request.Quantity,
            Applied: true,
            remaining);
    }

    /// <summary>
    /// The compensating half of a reserve. A release for an order that never reserved, or
    /// that has already been released, is a no-op that reports Applied = false, which is what
    /// lets Order retry compensation after a failure without over-crediting stock.
    /// </summary>
    public async Task<StockReservationResponse> ReleaseAsync(StockMutationRequest request, CancellationToken cancellationToken)
    {
        var existing = await _reservationRepository.FindAsync(request.OrderId, request.ProductId, cancellationToken);

        var inventory = await GetInventoryOrThrowAsync(request.ProductId, cancellationToken);

        if (existing is null || existing.ReleasedAt.HasValue)
        {
            _logger.LogInformation(
                "Release for order {OrderId} product {ProductId} had nothing to release; stock untouched",
                request.OrderId,
                request.ProductId);

            return new StockReservationResponse(
                request.OrderId,
                request.ProductId,
                existing?.Quantity ?? request.Quantity,
                Applied: false,
                inventory.Quantity);
        }

        var remaining = inventory.Quantity + existing.Quantity;

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            inventory.Quantity = remaining;
            inventory.UpdatedAt = DateTime.UtcNow;
            _inventoryRepository.Update(inventory);

            existing.ReleasedAt = DateTime.UtcNow;

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        _logger.LogInformation(
            "Released {Quantity} unit(s) of product {ProductId} for order {OrderId}; {Remaining} in stock",
            existing.Quantity,
            request.ProductId,
            request.OrderId,
            remaining);

        return new StockReservationResponse(
            request.OrderId,
            request.ProductId,
            existing.Quantity,
            Applied: true,
            remaining);
    }

    private static InventoryResponse ToResponse(Inventory inventory) => new(
        inventory.ProductId,
        inventory.Product.Name,
        inventory.Product.Sku,
        inventory.Quantity,
        inventory.Quantity > 0,
        inventory.UpdatedAt);

    private static InventorySnapshotResponse ToSnapshot(Inventory inventory) => new(
        inventory.ProductId,
        inventory.Product.Name,
        inventory.Product.Sku,
        inventory.Product.ImageUrl,
        inventory.Product.Price,
        inventory.Product.IsActive,
        inventory.Quantity,
        inventory.UpdatedAt);
}
