using ECommerce.Application.DTOs.Cart;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Interfaces;
using ECommerce.Application.Services;
using ECommerce.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ECommerce.UnitTests.Services;

public class CartServiceTests
{
    private readonly Mock<ICartRepository> _cartRepository = new();
    private readonly Mock<IInventoryRepository> _inventoryRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly CartService _service;

    public CartServiceTests()
    {
        _service = new CartService(
            _cartRepository.Object,
            _inventoryRepository.Object,
            _unitOfWork.Object,
            NullLogger<CartService>.Instance);
    }

    /// <summary>
    /// Builds an inventory row whose Product.Inventory points back at it, so mutating
    /// either side is visible to the service.
    /// </summary>
    private static Inventory CreateInventory(
        int productId = 10,
        decimal price = 50m,
        int stock = 20,
        bool isActive = true)
    {
        var product = new Product
        {
            Id = productId,
            Name = "Pro Laptop",
            Sku = "LAP-1",
            Price = price,
            IsActive = isActive
        };

        var inventory = new Inventory
        {
            Id = productId,
            ProductId = productId,
            Quantity = stock,
            Product = product
        };

        product.Inventory = inventory;

        return inventory;
    }

    private static CartItem CreateCartItem(Product product, int quantity = 1, int itemId = 100) => new()
    {
        Id = itemId,
        ProductId = product.Id,
        Quantity = quantity,
        Product = product
    };

    private static Cart CreateCart(int userId = 1, params CartItem[] items) => new()
    {
        Id = 5,
        UserId = userId,
        CreatedAt = DateTime.UtcNow.AddDays(-1),
        UpdatedAt = DateTime.UtcNow,
        Items = new List<CartItem>(items)
    };

    private void SetupCart(Cart? cart)
    {
        _cartRepository
            .Setup(x => x.GetByUserIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);
    }

    [Fact]
    public async Task GetCartAsync_ReturnsEmptyCart_WhenNoCartExists()
    {
        SetupCart(null);

        var result = await _service.GetCartAsync(1, CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(0m, result.Subtotal);
        Assert.Equal(0, result.TotalQuantity);
    }

    [Fact]
    public async Task GetCartAsync_DoesNotCreateCartRow_WhenNoCartExists()
    {
        SetupCart(null);

        await _service.GetCartAsync(1, CancellationToken.None);

        _cartRepository.Verify(x => x.Add(It.IsAny<Cart>()), Times.Never);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetCartAsync_CalculatesTotalsFromProductPrice()
    {
        var inventory = CreateInventory(price: 50m, stock: 20);
        SetupCart(CreateCart(1, CreateCartItem(inventory.Product, 3)));

        var result = await _service.GetCartAsync(1, CancellationToken.None);

        Assert.Equal(150m, result.Subtotal);
        Assert.Equal(3, result.TotalQuantity);
        Assert.Equal(150m, result.Items[0].LineTotal);
    }

    [Fact]
    public async Task GetCartAsync_UsesLiveProductPrice_NotAStoredPrice()
    {
        var inventory = CreateInventory(price: 50m, stock: 20);
        SetupCart(CreateCart(1, CreateCartItem(inventory.Product, 2)));

        var first = await _service.GetCartAsync(1, CancellationToken.None);

        // The price lives only on the product row, so a later price change is reflected.
        inventory.Product.Price = 80m;

        var second = await _service.GetCartAsync(1, CancellationToken.None);

        Assert.Equal(100m, first.Subtotal);
        Assert.Equal(160m, second.Subtotal);
    }

    [Fact]
    public async Task GetCartAsync_ReportsAvailableStock()
    {
        var inventory = CreateInventory(stock: 7);
        SetupCart(CreateCart(1, CreateCartItem(inventory.Product, 1)));

        var result = await _service.GetCartAsync(1, CancellationToken.None);

        Assert.Equal(7, result.Items[0].AvailableStock);
    }

    [Fact]
    public async Task AddItemAsync_CreatesCartWhenNoneExists()
    {
        SetupCart(null);
        var inventory = CreateInventory(stock: 20);
        _inventoryRepository
            .Setup(x => x.GetByProductIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inventory);

        var result = await _service.AddItemAsync(
            1,
            new AddCartItemRequest { ProductId = 10, Quantity = 2 },
            CancellationToken.None);

        _cartRepository.Verify(x => x.Add(It.Is<Cart>(c => c.UserId == 1)), Times.Once);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.Single(result.Items);
        Assert.Equal(2, result.Items[0].Quantity);
    }

    [Fact]
    public async Task AddItemAsync_IncrementsExistingLine_ForSameProduct()
    {
        var inventory = CreateInventory(stock: 20);
        var item = CreateCartItem(inventory.Product, 2);
        SetupCart(CreateCart(1, item));
        _inventoryRepository
            .Setup(x => x.GetByProductIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inventory);

        var result = await _service.AddItemAsync(
            1,
            new AddCartItemRequest { ProductId = 10, Quantity = 3 },
            CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(5, result.Items[0].Quantity);
    }

    [Fact]
    public async Task AddItemAsync_ThrowsNotFound_WhenProductDoesNotExist()
    {
        SetupCart(CreateCart());
        _inventoryRepository
            .Setup(x => x.GetByProductIdAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Inventory?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.AddItemAsync(
            1,
            new AddCartItemRequest { ProductId = 99, Quantity = 1 },
            CancellationToken.None));
    }

    [Fact]
    public async Task AddItemAsync_ThrowsValidation_WhenProductIsInactive()
    {
        var inventory = CreateInventory(isActive: false);
        SetupCart(CreateCart());
        _inventoryRepository
            .Setup(x => x.GetByProductIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inventory);

        await Assert.ThrowsAsync<ValidationException>(() => _service.AddItemAsync(
            1,
            new AddCartItemRequest { ProductId = 10, Quantity = 1 },
            CancellationToken.None));
    }

    [Fact]
    public async Task AddItemAsync_ThrowsValidation_WhenRequestedQuantityExceedsStock()
    {
        var inventory = CreateInventory(stock: 3);
        SetupCart(CreateCart());
        _inventoryRepository
            .Setup(x => x.GetByProductIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inventory);

        var exception = await Assert.ThrowsAsync<ValidationException>(() => _service.AddItemAsync(
            1,
            new AddCartItemRequest { ProductId = 10, Quantity = 4 },
            CancellationToken.None));

        Assert.Contains("3 unit(s)", exception.Message);
    }

    [Fact]
    public async Task AddItemAsync_ThrowsValidation_WhenCombinedQuantityExceedsStock()
    {
        var inventory = CreateInventory(stock: 5);
        SetupCart(CreateCart(1, CreateCartItem(inventory.Product, 4)));
        _inventoryRepository
            .Setup(x => x.GetByProductIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inventory);

        await Assert.ThrowsAsync<ValidationException>(() => _service.AddItemAsync(
            1,
            new AddCartItemRequest { ProductId = 10, Quantity = 2 },
            CancellationToken.None));
    }

    [Fact]
    public async Task AddItemAsync_ThrowsValidation_WhenLineLimitExceeded()
    {
        var inventory = CreateInventory(stock: 500);
        SetupCart(CreateCart(1, CreateCartItem(inventory.Product, 95)));
        _inventoryRepository
            .Setup(x => x.GetByProductIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inventory);

        await Assert.ThrowsAsync<ValidationException>(() => _service.AddItemAsync(
            1,
            new AddCartItemRequest { ProductId = 10, Quantity = 10 },
            CancellationToken.None));
    }

    [Fact]
    public async Task UpdateItemAsync_SetsExactQuantity()
    {
        var inventory = CreateInventory(stock: 20);
        SetupCart(CreateCart(1, CreateCartItem(inventory.Product, 2)));

        var result = await _service.UpdateItemAsync(1, 100, new UpdateCartItemRequest { Quantity = 6 }, CancellationToken.None);

        Assert.Equal(6, result.Items[0].Quantity);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateItemAsync_ThrowsValidation_WhenQuantityExceedsStock()
    {
        var inventory = CreateInventory(stock: 3);
        SetupCart(CreateCart(1, CreateCartItem(inventory.Product, 1)));

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.UpdateItemAsync(1, 100, new UpdateCartItemRequest { Quantity = 4 }, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateItemAsync_ThrowsNotFound_WhenItemNotInCart()
    {
        var inventory = CreateInventory();
        SetupCart(CreateCart(1, CreateCartItem(inventory.Product, 1, itemId: 100)));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.UpdateItemAsync(1, 999, new UpdateCartItemRequest { Quantity = 1 }, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateItemAsync_ThrowsNotFound_WhenCartMissing()
    {
        SetupCart(null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.UpdateItemAsync(1, 100, new UpdateCartItemRequest { Quantity = 1 }, CancellationToken.None));
    }

    [Fact]
    public async Task RemoveItemAsync_RemovesTheLine()
    {
        var first = CreateInventory(productId: 10);
        var second = CreateInventory(productId: 11, price: 25m, stock: 4);
        SetupCart(CreateCart(1, CreateCartItem(first.Product, 1, itemId: 100), CreateCartItem(second.Product, 1, itemId: 101)));

        var result = await _service.RemoveItemAsync(1, 100, CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(11, result.Items[0].ProductId);
    }

    [Fact]
    public async Task RemoveItemAsync_ThrowsNotFound_WhenItemNotInCart()
    {
        var inventory = CreateInventory();
        SetupCart(CreateCart(1, CreateCartItem(inventory.Product, 1, itemId: 100)));

        await Assert.ThrowsAsync<NotFoundException>(() => _service.RemoveItemAsync(1, 999, CancellationToken.None));
    }

    [Fact]
    public async Task ClearCartAsync_RemovesEveryLine()
    {
        var first = CreateInventory(productId: 10);
        var second = CreateInventory(productId: 11);
        SetupCart(CreateCart(1, CreateCartItem(first.Product), CreateCartItem(second.Product)));

        await _service.ClearCartAsync(1, CancellationToken.None);

        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ClearCartAsync_DoesNothing_WhenCartMissing()
    {
        SetupCart(null);

        await _service.ClearCartAsync(1, CancellationToken.None);

        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ClearCartAsync_DoesNothing_WhenCartAlreadyEmpty()
    {
        SetupCart(CreateCart());

        await _service.ClearCartAsync(1, CancellationToken.None);

        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
