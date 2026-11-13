using ECommerce.Orders.Application.DTOs.Cart;
using ECommerce.Orders.Application.DTOs.Products;
using ECommerce.Shared.Exceptions;
using ECommerce.Orders.Application.Interfaces;
using ECommerce.Orders.Domain.Entities;
using Microsoft.Extensions.Logging;
using ECommerce.Shared.Data;

namespace ECommerce.Orders.Application.Services;

public class CartService
{
    private const int MaxQuantityPerLine = 100;

    private readonly ICartRepository _cartRepository;
    private readonly IProductServiceClient _productClient;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CartService> _logger;

    public CartService(
        ICartRepository cartRepository,
        IProductServiceClient productClient,
        IUnitOfWork unitOfWork,
        ILogger<CartService> logger)
    {
        _cartRepository = cartRepository;
        _productClient = productClient;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<CartResponse> GetCartAsync(int userId, CancellationToken cancellationToken)
    {
        var cart = await _cartRepository.GetByUserIdAsync(userId, cancellationToken);

        // A missing cart row just means nothing has been added yet, so nothing is written.
        return cart is null ? EmptyCart() : await ToResponseAsync(cart, knownSnapshot: null, cancellationToken);
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
        var product = await GetSellableProductOrThrowAsync(request.ProductId, cancellationToken);
        var item = cart.Items.FirstOrDefault(x => x.ProductId == request.ProductId);

        var desiredQuantity = (item?.Quantity ?? 0) + request.Quantity;
        EnsureQuantityAllowed(desiredQuantity, product.Quantity, product.Name);

        if (item is null)
        {
            cart.Items.Add(new CartItem
            {
                ProductId = product.ProductId,
                Quantity = request.Quantity
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

        return await ToResponseAsync(cart, product, cancellationToken);
    }

    public async Task<CartResponse> UpdateItemAsync(
        int userId,
        int itemId,
        UpdateCartItemRequest request,
        CancellationToken cancellationToken)
    {
        var cart = await GetCartOrThrowAsync(userId, cancellationToken);
        var item = FindItemOrThrow(cart, itemId);

        // Re-reads the product rather than trusting what is on the cart line: availability is
        // Product's answer to give, and it can have changed since the line was written.
        var product = await GetSellableProductOrThrowAsync(item.ProductId, cancellationToken);

        EnsureQuantityAllowed(request.Quantity, product.Quantity, product.Name);

        item.Quantity = request.Quantity;
        cart.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} set cart item {ItemId} to {Quantity}", userId, itemId, request.Quantity);

        return await ToResponseAsync(cart, product, cancellationToken);
    }

    public async Task<CartResponse> RemoveItemAsync(int userId, int itemId, CancellationToken cancellationToken)
    {
        var cart = await GetCartOrThrowAsync(userId, cancellationToken);
        var item = FindItemOrThrow(cart, itemId);

        cart.Items.Remove(item);
        cart.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} removed cart item {ItemId}", userId, itemId);

        return await ToResponseAsync(cart, knownSnapshot: null, cancellationToken);
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

    /// <summary>
    /// Product Service answers whether the product exists and is sellable. A missing product
    /// is a 404 from Product, which the client surfaces as a NotFoundException, so the
    /// storefront sees the same answer it saw when this was a local database read.
    /// </summary>
    private async Task<InventorySnapshot> GetSellableProductOrThrowAsync(int productId, CancellationToken cancellationToken)
    {
        var product = await _productClient.GetInventorySnapshotAsync(productId, cancellationToken);

        if (!product.IsActive)
        {
            throw new ValidationException($"Product '{product.Name}' is no longer available.");
        }

        return product;
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

    /// <summary>
    /// Prices and stock on a cart line are always Product's current answer, so each distinct
    /// product on the cart costs one snapshot call. <paramref name="knownSnapshot"/> is passed
    /// in when the caller has just fetched that product, which avoids reading it twice.
    /// </summary>
    private async Task<CartResponse> ToResponseAsync(
        Cart cart,
        InventorySnapshot? knownSnapshot,
        CancellationToken cancellationToken)
    {
        var snapshots = new Dictionary<int, InventorySnapshot>();

        if (knownSnapshot is not null)
        {
            snapshots[knownSnapshot.ProductId] = knownSnapshot;
        }

        foreach (var productId in cart.Items.Select(x => x.ProductId).Distinct())
        {
            if (!snapshots.ContainsKey(productId))
            {
                snapshots[productId] = await _productClient.GetInventorySnapshotAsync(productId, cancellationToken);
            }
        }

        var items = cart.Items.Select(x => ToItemResponse(x, snapshots[x.ProductId])).ToList();

        return new CartResponse(
            cart.Id,
            items,
            items.Sum(x => x.LineTotal),
            items.Sum(x => x.Quantity));
    }

    private static CartItemResponse ToItemResponse(CartItem item, InventorySnapshot product) => new(
        item.Id,
        item.ProductId,
        product.Name,
        product.Sku,
        product.ImageUrl,
        product.Price,
        item.Quantity,
        product.Quantity,
        product.Price * item.Quantity);
}
