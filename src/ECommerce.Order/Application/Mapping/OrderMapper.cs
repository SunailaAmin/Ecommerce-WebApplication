using ECommerce.Orders.Application.DTOs.Orders;
using ECommerce.Orders.Application.DTOs.Products;
using ECommerce.Orders.Application.Interfaces;
using ECommerce.Orders.Domain.Entities;
using ECommerce.Shared.DTOs;

namespace ECommerce.Orders.Application.Mapping;

/// <summary>
/// Shared order mapping, used by order queries and by checkout so an order is never shaped
/// two different ways. Product name and SKU are not stored on the order, so they are read
/// from Product Service here: this keeps order history showing the same live product
/// description it showed when Order and Product shared one database.
/// </summary>
public static class OrderMapper
{
    public static async Task<OrderResponse> ToResponseAsync(
        this Order order,
        IProductServiceClient productClient,
        CancellationToken cancellationToken)
    {
        var snapshots = await GetSnapshotsAsync(order.Items, productClient, cancellationToken);

        return new OrderResponse(
            order.Id,
            order.OrderNumber,
            order.UserId,
            $"{order.User.FirstName} {order.User.LastName}".Trim(),
            order.User.Email,
            order.TotalAmount,
            order.Status,
            order.ShippingAddress.ToResponse(),
            order.Items.Select(x => ToItemResponse(x, snapshots)).ToList(),
            order.CreatedAt,
            order.UpdatedAt);
    }

    public static async Task<PagedResult<OrderResponse>> ToResponseAsync(
        this PagedResult<Order> page,
        IProductServiceClient productClient,
        CancellationToken cancellationToken)
    {
        var items = new List<OrderResponse>(page.Items.Count);

        foreach (var order in page.Items)
        {
            items.Add(await order.ToResponseAsync(productClient, cancellationToken));
        }

        return new PagedResult<OrderResponse>(items, page.TotalCount, page.Page, page.PageSize);
    }

    private static async Task<IReadOnlyDictionary<int, InventorySnapshot>> GetSnapshotsAsync(
        IEnumerable<OrderItem> items,
        IProductServiceClient productClient,
        CancellationToken cancellationToken)
    {
        var productIds = items.Select(x => x.ProductId).Distinct().ToList();
        var snapshots = new Dictionary<int, InventorySnapshot>(productIds.Count);

        foreach (var productId in productIds)
        {
            snapshots[productId] = await productClient.GetInventorySnapshotAsync(productId, cancellationToken);
        }

        return snapshots;
    }

    private static OrderItemResponse ToItemResponse(
        OrderItem item,
        IReadOnlyDictionary<int, InventorySnapshot> snapshots)
    {
        var product = snapshots[item.ProductId];

        return new OrderItemResponse(
            item.Id,
            item.ProductId,
            product.Name,
            product.Sku,
            item.Quantity,
            item.UnitPrice,
            item.TotalPrice);
    }
}
