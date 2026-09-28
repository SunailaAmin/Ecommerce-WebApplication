using ECommerce.Application.DTOs;
using ECommerce.Application.DTOs.Orders;
using ECommerce.Application.Interfaces;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Infrastructure.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly ECommerceDbContext _context;

    public OrderRepository(ECommerceDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<Order>> GetPagedForUserAsync(
        int userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var orders = WithDetails(
            _context.Orders
                .AsNoTracking())
            .Where(x => x.UserId == userId);

        var totalCount = await orders.CountAsync(cancellationToken);

        var items = await orders
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Order>(items, totalCount, page, pageSize);
    }

    public async Task<PagedResult<Order>> GetPagedAsync(OrderQuery query, CancellationToken cancellationToken)
    {
        var orders = WithDetails(
            _context.Orders
                .AsNoTracking())
            .AsQueryable();

        if (query.Status.HasValue)
        {
            orders = orders.Where(x => x.Status == query.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            orders = orders.Where(x =>
                x.OrderNumber.Contains(search)
                || x.User.Email.Contains(search)
                || x.User.FirstName.Contains(search)
                || x.User.LastName.Contains(search));
        }

        var totalCount = await orders.CountAsync(cancellationToken);

        var sorted = Sort(orders, query.SortBy, query.SortDescending);

        var items = await sorted
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Order>(items, totalCount, query.Page, query.PageSize);
    }

    public Task<Order?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        // Payments are included because cancelling an order cancels its unsettled
        // payments, which happens on this path.
        return WithDetails(_context.Orders)
            .Include(x => x.Payments)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<bool> OrderNumberExistsAsync(string orderNumber, CancellationToken cancellationToken)
    {
        return _context.Orders.AnyAsync(x => x.OrderNumber == orderNumber, cancellationToken);
    }

    public Task<List<Order>> GetPendingCreatedBeforeAsync(DateTime createdBefore, CancellationToken cancellationToken)
    {
        // Included for the same reason as GetByIdAsync: the cleanup job cancels the order
        // and therefore its unsettled payments.
        return WithDetails(_context.Orders)
            .Include(x => x.Payments)
            .Where(x => x.Status == OrderStatus.Pending && x.CreatedAt < createdBefore)
            .ToListAsync(cancellationToken);
    }

    public void Add(Order order)
    {
        _context.Orders.Add(order);
    }

    private static IQueryable<Order> WithDetails(IQueryable<Order> orders)
    {
        return orders
            .Include(x => x.User)
            .Include(x => x.ShippingAddress)
            .Include(x => x.Items)
                .ThenInclude(x => x.Product)
                    .ThenInclude(x => x.Inventory);
    }

    private static IQueryable<Order> Sort(IQueryable<Order> orders, string? sortBy, bool descending)
    {
        return sortBy?.ToLowerInvariant() switch
        {
            "totalamount" => descending
                ? orders.OrderByDescending(x => x.TotalAmount)
                : orders.OrderBy(x => x.TotalAmount),
            "status" => descending
                ? orders.OrderByDescending(x => x.Status)
                : orders.OrderBy(x => x.Status),
            _ => descending
                ? orders.OrderByDescending(x => x.CreatedAt)
                : orders.OrderBy(x => x.CreatedAt)
        };
    }
}
