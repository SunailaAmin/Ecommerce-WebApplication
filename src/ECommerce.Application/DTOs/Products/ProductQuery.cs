using System.ComponentModel.DataAnnotations;

namespace ECommerce.Application.DTOs.Products;

public class ProductQuery
{
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 10;

    /// <summary>
    /// Matches product name or SKU.
    /// </summary>
    public string? Search { get; set; }

    public int? CategoryId { get; set; }

    /// <summary>
    /// Include deactivated products. Intended for admin use.
    /// </summary>
    public bool IncludeInactive { get; set; }

    /// <summary>
    /// One of: name, price, createdat.
    /// </summary>
    public string SortBy { get; set; } = "name";

    public bool SortDescending { get; set; }
}
