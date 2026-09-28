using ECommerce.Application.DTOs;
using ECommerce.Application.DTOs.Inventory;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Interfaces;
using ECommerce.Application.Services;
using ECommerce.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ECommerce.UnitTests.Services;

public class InventoryServiceTests
{
    private readonly Mock<IInventoryRepository> _inventoryRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly InventoryService _service;

    public InventoryServiceTests()
    {
        _service = new InventoryService(
            _inventoryRepository.Object,
            _unitOfWork.Object,
            NullLogger<InventoryService>.Instance);
    }

    private static Inventory CreateInventory(int productId = 10, int quantity = 20) => new()
    {
        Id = productId,
        ProductId = productId,
        Quantity = quantity,
        Product = new Product
        {
            Id = productId,
            Name = "Pro Laptop",
            Sku = "LAP-1"
        }
    };

    [Fact]
    public async Task GetInventoryAsync_ReturnsPagedInventory()
    {
        var query = new InventoryQuery { Page = 1, PageSize = 10 };
        var items = new List<Inventory> { CreateInventory() };

        _inventoryRepository
            .Setup(x => x.GetPagedAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<Inventory>(items, 1, 1, 10));

        var result = await _service.GetInventoryAsync(query, CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal("Pro Laptop", result.Items[0].ProductName);
        Assert.True(result.Items[0].IsInStock);
    }

    [Fact]
    public async Task GetInventoryByProductIdAsync_ReturnsQuantity()
    {
        _inventoryRepository
            .Setup(x => x.GetByProductIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateInventory(quantity: 5));

        var result = await _service.GetInventoryByProductIdAsync(10, CancellationToken.None);

        Assert.Equal(5, result.Quantity);
        Assert.True(result.IsInStock);
    }

    [Fact]
    public async Task GetInventoryByProductIdAsync_ReturnsNotInStock_WhenQuantityIsZero()
    {
        _inventoryRepository
            .Setup(x => x.GetByProductIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateInventory(quantity: 0));

        var result = await _service.GetInventoryByProductIdAsync(10, CancellationToken.None);

        Assert.False(result.IsInStock);
    }

    [Fact]
    public async Task GetInventoryByProductIdAsync_ThrowsNotFound_WhenNoInventoryRecord()
    {
        _inventoryRepository
            .Setup(x => x.GetByProductIdAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Inventory?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.GetInventoryByProductIdAsync(99, CancellationToken.None));
    }

    [Fact]
    public async Task SetQuantityAsync_ReplacesQuantity()
    {
        var inventory = CreateInventory(quantity: 20);
        _inventoryRepository
            .Setup(x => x.GetByProductIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inventory);

        var result = await _service.SetQuantityAsync(10, new UpdateInventoryRequest { Quantity = 50 }, CancellationToken.None);

        Assert.Equal(50, result.Quantity);
        _inventoryRepository.Verify(x => x.Update(inventory), Times.Once);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AdjustQuantityAsync_IncreasesStock()
    {
        var inventory = CreateInventory(quantity: 20);
        _inventoryRepository
            .Setup(x => x.GetByProductIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inventory);

        var result = await _service.AdjustQuantityAsync(10, new AdjustInventoryRequest { Delta = 5 }, CancellationToken.None);

        Assert.Equal(25, result.Quantity);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AdjustQuantityAsync_DecreasesStock()
    {
        var inventory = CreateInventory(quantity: 20);
        _inventoryRepository
            .Setup(x => x.GetByProductIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inventory);

        var result = await _service.AdjustQuantityAsync(10, new AdjustInventoryRequest { Delta = -8 }, CancellationToken.None);

        Assert.Equal(12, result.Quantity);
    }

    [Fact]
    public async Task AdjustQuantityAsync_ThrowsValidation_WhenResultWouldBeNegative()
    {
        var inventory = CreateInventory(quantity: 3);
        _inventoryRepository
            .Setup(x => x.GetByProductIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inventory);

        await Assert.ThrowsAsync<ValidationException>(
            () => _service.AdjustQuantityAsync(10, new AdjustInventoryRequest { Delta = -10 }, CancellationToken.None));

        Assert.Equal(3, inventory.Quantity);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AdjustQuantityAsync_ThrowsValidation_WhenDeltaIsZero()
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => _service.AdjustQuantityAsync(10, new AdjustInventoryRequest { Delta = 0 }, CancellationToken.None));

        _inventoryRepository.Verify(
            x => x.GetByProductIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AdjustQuantityAsync_ThrowsNotFound_WhenNoInventoryRecord()
    {
        _inventoryRepository
            .Setup(x => x.GetByProductIdAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Inventory?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.AdjustQuantityAsync(99, new AdjustInventoryRequest { Delta = 1 }, CancellationToken.None));
    }
}
