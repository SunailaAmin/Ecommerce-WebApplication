using System.ComponentModel.DataAnnotations;

namespace ECommerce.Products.Application.DTOs.Inventory;

public class InventoryQuery
{
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 10;

    /// <summary>
    /// Matches product name or SKU.
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    /// Only return products at or below the given quantity.
    /// </summary>
    public int? LowStockAtOrBelow { get; set; }
}
