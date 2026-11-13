using ECommerce.Orders.Application.DTOs;
using ECommerce.Orders.Application.DTOs.Orders;
using ECommerce.Orders.Domain.Entities;
using ECommerce.Shared.DTOs;

namespace ECommerce.Orders.Application.Interfaces;

public interface IOrderRepository
{
    /// <summary>
    /// Paged order history for a single customer, newest first.
    /// </summary>
    Task<PagedResult<Order>> GetPagedForUserAsync(int userId, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>
    /// Paged order list across all customers. Intended for admin use.
    /// </summary>
    Task<PagedResult<Order>> GetPagedAsync(OrderQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the order with items, products, shipping address and customer loaded.
    /// Tracked for update, so status changes need no explicit Update call.
    /// </summary>
    Task<Order?> GetByIdAsync(int id, CancellationToken cancellationToken);

    /// <summary>
    /// Used to keep generated order numbers unique against the unique index.
    /// </summary>
    Task<bool> OrderNumberExistsAsync(string orderNumber, CancellationToken cancellationToken);

    /// <summary>
    /// Pending orders created before the cutoff, with the data needed to cancel them.
    /// </summary>
    Task<List<Order>> GetPendingCreatedBeforeAsync(DateTime createdBefore, CancellationToken cancellationToken);

    void Add(Order order);
}
