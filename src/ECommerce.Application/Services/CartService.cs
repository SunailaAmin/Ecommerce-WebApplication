using ECommerce.Application.DTOs.Cart;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Interfaces;
using ECommerce.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ECommerce.Application.Services;

public class CartService
{
    private const int MaxQuantityPerLine = 100;

    private readonly ICartRepository _cartRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CartService> _logger;

    public CartService(
        ICartRepository cartRepository,
        IInventoryRepository inventoryRepository,
        IUnitOfWork unitOfWork,
        ILogger<CartService> logger)
    {
        _cartRepository = cartRepository;
        _inventoryRepository = inventoryRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<CartResponse> GetCartAsync(int userId, CancellationToken cancellationToken)
    {
        var cart = await _cartRepository.GetByUserIdAsync(userId, cancellationToken);

        // A missing cart row just means nothing has been added yet, so nothing is written.
        return cart is null ? EmptyCart() : ToResponse(cart);
    }

    /// <summary>
    /// Adds units of a product. Adding a product that is already in the cart increases
    /// the existing line rather than creating a duplicate.
    /// </summary>
    public async Task<CartResponse> AddItemAsync(
        int userId,
        AddCartItemRequest request,
        CancellationToken cancellationToken)
    {
        var cart = await GetOrCreateCartAsync(userId, cancellationToken);
        var inventory = await GetSellableInventoryOrThrowAsync(request.ProductId, cancellationToken);
        var item = cart.Items.FirstOrDefault(x => x.ProductId == request.ProductId);

        var desiredQuantity = (item?.Quantity ?? 0) + request.Quantity;
        EnsureQuantityAllowed(desiredQuantity, inventory.Quantity, inventory.Product.Name);

        if (item is null)
        {
            cart.Items.Add(new CartItem
            {
                ProductId = inventory.ProductId,
                Quantity = request.Quantity,
                Product = inventory.Product
            });
        }
        else
        {
            item.Quantity = desiredQuantity;
        }

        cart.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "User {UserId} added {Quantity} unit(s) of product {ProductId} to the cart",
            userId,
            request.Quantity,
            request.ProductId);

        return ToResponse(cart);
    }

    public async Task<CartResponse> UpdateItemAsync(
        int userId,
        int itemId,
        UpdateCartItemRequest request,
        CancellationToken cancellationToken)
    {
        var cart = await GetCartOrThrowAsync(userId, cancellationToken);
        var item = FindItemOrThrow(cart, itemId);

        EnsureQuantityAllowed(request.Quantity, item.Product.Inventory?.Quantity ?? 0, item.Product.Name);

        item.Quantity = request.Quantity;
        cart.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} set cart item {ItemId} to {Quantity}", userId, itemId, request.Quantity);

        return ToResponse(cart);
    }

    public async Task<CartResponse> RemoveItemAsync(int userId, int itemId, CancellationToken cancellationToken)
    {
        var cart = await GetCartOrThrowAsync(userId, cancellationToken);
        var item = FindItemOrThrow(cart, itemId);

        cart.Items.Remove(item);
        cart.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} removed cart item {ItemId}", userId, itemId);

        return ToResponse(cart);
    }

    public async Task ClearCartAsync(int userId, CancellationToken cancellationToken)
    {
        var cart = await _cartRepository.GetByUserIdAsync(userId, cancellationToken);

        if (cart is null || cart.Items.Count == 0)
        {
            return;
        }

        var removedCount = cart.Items.Count;

        cart.Items.Clear();
        cart.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Cleared {Count} line(s) from the cart of user {UserId}", removedCount, userId);
    }

    private async Task<Cart> GetOrCreateCartAsync(int userId, CancellationToken cancellationToken)
    {
        var cart = await _cartRepository.GetByUserIdAsync(userId, cancellationToken);

        if (cart is not null)
        {
            return cart;
        }

        var now = DateTime.UtcNow;

        cart = new Cart
        {
            UserId = userId,
            CreatedAt = now,
            UpdatedAt = now
        };

        _cartRepository.Add(cart);

        return cart;
    }

    private async Task<Cart> GetCartOrThrowAsync(int userId, CancellationToken cancellationToken)
    {
        return await _cartRepository.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Your cart is empty.");
    }

    private static CartItem FindItemOrThrow(Cart cart, int itemId)
    {
        return cart.Items.FirstOrDefault(x => x.Id == itemId)
            ?? throw new NotFoundException($"Cart item with id {itemId} was not found in your cart.");
    }

    private async Task<Inventory> GetSellableInventoryOrThrowAsync(int productId, CancellationToken cancellationToken)
    {
        var inventory = await _inventoryRepository.GetByProductIdAsync(productId, cancellationToken)
            ?? throw new NotFoundException($"Product with id {productId} was not found.");

        if (!inventory.Product.IsActive)
        {
            throw new ValidationException($"Product '{inventory.Product.Name}' is no longer available.");
        }

        return inventory;
    }

    private static void EnsureQuantityAllowed(int desiredQuantity, int availableQuantity, string productName)
    {
        if (desiredQuantity > MaxQuantityPerLine)
        {
            throw new ValidationException($"A single cart line is limited to {MaxQuantityPerLine} units.");
        }

        if (desiredQuantity > availableQuantity)
        {
            throw new ValidationException(
                $"Only {availableQuantity} unit(s) of '{productName}' are in stock.");
        }
    }

    private static CartResponse EmptyCart() => new(0, [], 0m, 0);

    private static CartResponse ToResponse(Cart cart)
    {
        var items = cart.Items.Select(ToItemResponse).ToList();

        return new CartResponse(
            cart.Id,
            items,
            items.Sum(x => x.LineTotal),
            items.Sum(x => x.Quantity));
    }

    private static CartItemResponse ToItemResponse(CartItem item) => new(
        item.Id,
        item.ProductId,
        item.Product.Name,
        item.Product.Sku,
        item.Product.ImageUrl,
        item.Product.Price,
        item.Quantity,
        item.Product.Inventory?.Quantity ?? 0,
        item.Product.Price * item.Quantity);
}
