using System.ComponentModel.DataAnnotations;
using ECommerce.Domain.Enums;

namespace ECommerce.Application.DTOs.Orders;

/// <summary>
/// Filtering for the admin order list. Customer order history uses paging only.
/// </summary>
public class OrderQuery
{
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 10;

    public OrderStatus? Status { get; set; }

    /// <summary>
    /// Matches order number, customer name or customer email.
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    /// One of: createdat, totalamount, status.
    /// </summary>
    public string SortBy { get; set; } = "createdat";

    public bool SortDescending { get; set; }
}
